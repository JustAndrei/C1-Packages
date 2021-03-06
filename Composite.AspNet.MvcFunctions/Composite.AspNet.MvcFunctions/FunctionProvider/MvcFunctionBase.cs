﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Routing;
using System.Xml;
using Composite.AspNet.MvcFunctions;
using Composite.C1Console.Security;
using Composite.Core.Extensions;
using Composite.Core.IO;
using Composite.Core.Routing;
using Composite.Core.Routing.Pages;
using Composite.Core.WebClient.Renderings.Page;
using Composite.Core.Xml;
using Composite.Data;
using Composite.Functions;

namespace Composite.Plugins.Functions.FunctionProviders.MvcFunctions
{
    internal abstract class MvcFunctionBase: IFunction
    {
        protected readonly FunctionCollection _functionCollection;
        private readonly IList<ParameterProfile> _parameters = new List<ParameterProfile>();

        protected List<Tuple<Type, IDataUrlMapper>> _dataUrlMappers = new List<Tuple<Type, IDataUrlMapper>>();

        protected MvcFunctionBase(string @namespace, string name, string description, FunctionCollection functionCollection)
        {
            Verify.ArgumentNotNullOrEmpty(@namespace, "namespace");
            Verify.ArgumentNotNullOrEmpty(name, "name");
            Verify.ArgumentNotNull(functionCollection, "functionCollection");

            Namespace = @namespace;
            Name = @name;
            Description = description;
            _functionCollection = functionCollection;
        }

        public string Name { get; protected set; }
        public string Namespace { get; protected set; }
        public string Description { get; protected set; }
        public bool RequireAsyncHandler { get; protected set; }

        protected abstract bool HandlesPathInfo { get; }

        protected abstract string GetMvcRoute(ParameterList parameters);

        protected abstract string GetBaseMvcRoute(ParameterList parameters);

        internal virtual IEnumerable<ParameterInfo> GetParameterInformation()
        {
            return Enumerable.Empty<ParameterInfo>();
        }

        public virtual void AddParameter(ParameterProfile parameterProfile)
        {
            _parameters.Add(parameterProfile);
        }

        public Type ReturnType
        {
            get { return typeof (XhtmlDocument); } 
        }

        public virtual IEnumerable<ParameterProfile> ParameterProfiles
        {
            get { return _parameters; }
        }


        public EntityToken EntityToken
        {
            get { return new MvcFunctionEntityToken(this); } 
        }

        public object Execute(ParameterList parameters, FunctionContextContainer context)
        {
            var route = GetMvcRoute(parameters);

            var routeData = GetRouteData(route, parameters);
            if (routeData == null)
            {
                return null;
            }

            bool routeResolved = false;

            XhtmlDocument result;

            try
            {

                result = ExecuteRoute(routeData, parameters, ref routeResolved);
            }
            catch (Exception ex)
            {
                if (ex is ThreadAbortException
                    || ex is HttpException
                    || ex is ThreadInterruptedException
                    || ex is StackOverflowException
                    || ex is OutOfMemoryException)
                {
                    throw;
                }

                throw new InvalidOperationException("Error executing route '{0}'".FormatWith(route), ex);
            }
            finally
            {
                if (routeResolved && HandlesPathInfo && C1PageRoute.GetPathInfo() != null)
                {
                    C1PageRoute.RegisterPathInfoUsage();
                }
            }

            if (result != null)
            {
                ProcessDocument(result, parameters);
            }

            return result;
        }

        public async Task<object> ExecuteAsync(ParameterList parameters, FunctionContextContainer context)
        {
            string virtualRoute = GetMvcRoute(parameters);
            var routeData = GetRouteData(virtualRoute, parameters);
            if (routeData == null)
            {
                return null;
            }

            CultureInfo culture = C1PageRoute.PageUrlData.LocalizationScope;
            var publicationScope = C1PageRoute.PageUrlData.PublicationScope;

            var httpContext = HttpContext.Current;
            var result = await ExecuteRouteAsync(routeData, httpContext, culture, publicationScope);

            if (result != null && C1PageRoute.GetPathInfo() != null)
            {
                C1PageRoute.RegisterPathInfoUsage();
            }

            return result;
        }

        public bool SupportsAsync { get; private set; }


        private XhtmlDocument ExecuteRoute(RouteData routeData, ParameterList parameters, ref bool routeResolved)
        {
            AttachDynamicDataUrlMappers();

            var parentContext = HttpContext.Current;

            using (var writer = new StringWriter())
            {
                var httpResponse = new HttpResponse(writer);
                var httpContext = new HttpContext(parentContext.Request, httpResponse);
                var requestContext = new RequestContext(new HttpContextWrapper(httpContext), routeData);

                httpContext.User = parentContext.User;

                var handler = routeData.RouteHandler.GetHttpHandler(requestContext);
                Verify.IsNotNull(handler, "No handler found for the function '{0}'", Namespace + "." + Name);

                try
                {
                    handler.ProcessRequest(httpContext);
                }
                catch (HttpException httpException)
                {
                    if (httpException.GetHttpCode() == 404)
                    {
                        routeResolved = false;
                        return null;
                    }

                    routeResolved = true;
                    throw;
                }
                catch (Exception ex)
                {
                    EmbedExceptionSourceCode(ex);
                    routeResolved = true;

                    throw;
                }
                
                string xhtml = writer.ToString();
                routeResolved = httpResponse.StatusCode != 404;

                if (httpResponse.IsRequestBeingRedirected)
                {
                    string redirectUrl = httpResponse.RedirectLocation;
                    if (ActionLinkHelper.IsRawActionLink(redirectUrl))
                    {
                        redirectUrl = ActionLinkHelper.ConvertActionLink(redirectUrl, requestContext, _functionCollection.RouteCollection);
                        redirectUrl = ActionLinkHelper.ControllerLinkToC1PageLink(redirectUrl, GetBaseMvcRoute(parameters));
                    }

                    HttpContext.Current.Response.Redirect(redirectUrl, false);
                    return null;
                }

                var document = ParseOutput(xhtml);
                ActionLinkHelper.ConvertActionLinks(document, requestContext, _functionCollection.RouteCollection);

                ProcessDocument(document, parameters);

                return document;
            }
        }

        private void AttachDynamicDataUrlMappers()
        {
            var currentPageId = PageRenderer.CurrentPageId;
            if (currentPageId == Guid.Empty)
            {
                return;
            }

            foreach (var mapperInfo in _dataUrlMappers)
            {
                DataUrls.RegisterDynamicDataUrlMapper(currentPageId, mapperInfo.Item1, mapperInfo.Item2);
            }
        }

        private void EmbedExceptionSourceCode(Exception ex)
        {
            if (ex is ThreadAbortException
                   || ex is StackOverflowException
                   || ex is OutOfMemoryException
                   || ex is ThreadInterruptedException)
            {
                return;
            }

            var stackTrace = new StackTrace(ex, true);

            
            foreach (var frame in stackTrace.GetFrames())
            {
                string fileName = frame.GetFileName();

                if (fileName != null && File.Exists(fileName))
                {
                    var sourceCode = C1File.ReadAllLines(fileName);

                    XhtmlErrorFormatter.EmbedSourceCodeInformation(ex, sourceCode, frame.GetFileLineNumber());
                    return;
                }
            }
        }

        private async Task<XhtmlDocument> ExecuteRouteAsync(RouteData routeData, HttpContext parentContext, CultureInfo culture, PublicationScope publicationScope)
        {
            if (HttpContext.Current == null)
            {
                HttpContext.Current = parentContext;
            }

            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture = culture;

            string xhtml;

            using (var writer = new StringWriter())
            {
                var httpResponse = new HttpResponse(writer);
                var httpContext = new HttpContext(parentContext.Request, httpResponse);
                var requestContext = new RequestContext(new HttpContextWrapper(httpContext), routeData);

                var handler = routeData.RouteHandler.GetHttpHandler(requestContext);
                Verify.IsNotNull(handler, "No handler found for the function '{0}'", Namespace + "." + Name);

                var asyncHandler = handler as IHttpAsyncHandler;
                Verify.IsNotNull(asyncHandler, "The handler class '{0}' does not implement IHttpAsyncHandler interface", handler.GetType());

                // var asyncResult = asyncHandler.BeginProcessRequest(httpContext, null, null);

                try
                {
                    using (new DataScope(publicationScope, culture))
                    {
                        await Task.Factory.FromAsync((a, b) => asyncHandler.BeginProcessRequest(httpContext, a, b), asyncHandler.EndProcessRequest, null);
                    }
                }
                catch (HttpException httpException)
                {
                    if (httpException.GetHttpCode() == 404)
                    {
                        return null;
                    }

                    // TODO: embed exception with the route path

                    throw;
                }

                xhtml = writer.ToString();
            }

            return ParseOutput(xhtml);
        }


        private RouteData GetRouteData(string virtualUrl, ParameterList parameters)
        {
            var routeData = RouteUtils.GetRouteDataByUrl(_functionCollection.RouteCollection, virtualUrl);

            foreach (var parameterName in parameters.AllParameterNames)
            {
                object value;
                if (parameters.TryGetParameter(parameterName, out value))
                {
                    routeData.Values.Add(parameterName, value);
                }
            }

            return routeData;
        }

        private void ProcessDocument(XhtmlDocument document, ParameterList parameters)
        {
            string baseControllerUrl = GetBaseMvcRoute(parameters);

            ActionLinkHelper.ControllerLinksToC1PageLinks(document, baseControllerUrl);
        }

        private XhtmlDocument ParseOutput(string xhtml)
        {
            try
            {
                return XhtmlDocument.ParseXhtmlFragment(xhtml);
            }
            catch (XmlException ex)
            {
                string[] codeLines = xhtml.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);

                XhtmlErrorFormatter.EmbedSouceCodeInformation(ex, codeLines, ex.LineNumber);

                throw;
            }
        }

       
        public virtual void UsePathInfoForRouting()
        {
            throw new NotSupportedException();
        }

        public virtual void AssignDynamicUrlMapper(Type dataType, IDataUrlMapper dataUrlMapper)
        {
            _dataUrlMappers.Add(new Tuple<Type, IDataUrlMapper>(dataType, dataUrlMapper));
        }
    }
}
