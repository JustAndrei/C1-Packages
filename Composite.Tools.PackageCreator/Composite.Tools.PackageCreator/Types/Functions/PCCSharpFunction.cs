using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Composite.C1Console.Security;
using Composite.Data.Types;
using Composite.Data;
using Composite.Core.ResourceSystem;

namespace Composite.Tools.PackageCreator.Types
{
	partial class PCFunctions
	{

		public static IEnumerable<IPackageCreatorItem> CreateCSharp(EntityToken entityToken)
		{
			if (entityToken is DataEntityToken)
			{
				DataEntityToken dataEntityToken = (DataEntityToken)entityToken;
				if (dataEntityToken.Data is IMethodBasedFunctionInfo)
				{
					IMethodBasedFunctionInfo data = (IMethodBasedFunctionInfo)dataEntityToken.Data;
					yield return new PCFunctions(data.Namespace + "." + data.UserMethodName);
					yield break;
				}
			}
		}

		public void PackCSharpFunction(PackageCreator pc)
		{
			IMethodBasedFunctionInfo csharpFunction;
			try
			{
				csharpFunction = (from i in DataFacade.GetData<IMethodBasedFunctionInfo>()
								  where i.Namespace + "." + i.UserMethodName == this.Name
								  select i).First();
			}
			catch (Exception)
			{
				throw new ArgumentException(string.Format(@"C# Function '{0}' doesn't exists", this.Name));
			}

			pc.AddData(csharpFunction);
		}
	}
	
}