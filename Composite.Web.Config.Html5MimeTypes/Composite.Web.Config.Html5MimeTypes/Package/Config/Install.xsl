<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
		xmlns:msxsl="urn:schemas-microsoft-com:xslt" exclude-result-prefixes="msxsl add set"
		xmlns:add="http://www.composite.net/add/1.0"
		xmlns:set="http://www.composite.net/set/1.0"
		>
	<xsl:output method="xml" indent="yes"/>

	<xsl:variable name="structure">
		<system.webServer>
			<staticContent>
				<add:remove add:key="fileExtension" fileExtension=".mp4" />
				<add:remove add:key="fileExtension" fileExtension=".m4v" />
				<add:remove add:key="fileExtension" fileExtension=".ogg" />
				<add:remove add:key="fileExtension" fileExtension=".ogv" />
				<add:remove add:key="fileExtension" fileExtension=".oga" />
				<add:remove add:key="fileExtension" fileExtension=".spx" />
				<add:remove add:key="fileExtension" fileExtension=".svg" />
				<add:remove add:key="fileExtension" fileExtension=".svgz" />
				<add:remove add:key="fileExtension" fileExtension=".eot" />
				<add:remove add:key="fileExtension" fileExtension=".otf" />
				<add:remove add:key="fileExtension" fileExtension=".woff" />
        <add:remove add:key="fileExtension" fileExtension=".woff2" />
				<add:remove add:key="fileExtension" fileExtension=".appcache" />
				<add:remove add:key="fileExtension" fileExtension=".less" />
				<add:mimeMap add:key="fileExtension" fileExtension=".mp4" mimeType="video/mp4" />
				<add:mimeMap add:key="fileExtension" fileExtension=".m4v" mimeType="video/m4v" />
				<add:mimeMap add:key="fileExtension" fileExtension=".ogg" mimeType="video/ogg" />
				<add:mimeMap add:key="fileExtension" fileExtension=".ogv" mimeType="video/ogg" />
				<add:mimeMap add:key="fileExtension" fileExtension=".oga" mimeType="audio/ogg" />
				<add:mimeMap add:key="fileExtension" fileExtension=".spx" mimeType="audio/ogg" />
				<add:mimeMap add:key="fileExtension" fileExtension=".svg" mimeType="image/svg+xml" />
				<add:mimeMap add:key="fileExtension" fileExtension=".svgz" mimeType="image/svg+xml" />
				<add:mimeMap add:key="fileExtension" fileExtension=".eot" mimeType="application/vnd.ms-fontobject" />
				<add:mimeMap add:key="fileExtension" fileExtension=".otf" mimeType="font/otf" />
				<add:mimeMap add:key="fileExtension" fileExtension=".woff" mimeType="font/x-woff" />
        <add:mimeMap add:key="fileExtension" fileExtension=".woff2" mimeType="application/font-woff2" />
				<add:mimeMap add:key="fileExtension" fileExtension=".appcache" mimeType="text/cache-manifest" />
				<add:mimeMap add:key="fileExtension" fileExtension=".less" mimeType="text/css" />
			</staticContent>
		</system.webServer>
	</xsl:variable>

	<!--Start Engine 1.0.2-->
	<xsl:template match="/">
		<xsl:variable name="xml">
			<xsl:apply-templates select="/" mode="Prepare" />
		</xsl:variable>
		<xsl:apply-templates select="msxsl:node-set($xml)/*" />
	</xsl:template>

	<xsl:template match="@* | node()" mode="Prepare">
		<xsl:param name="nodes" select="$structure"/>
		<xsl:copy>
			<xsl:apply-templates select="@*" mode="Prepare" />
			<xsl:variable name="input" select="." />
			<xsl:apply-templates select="msxsl:node-set($nodes)/@set:*" mode="Copy" />
			<xsl:for-each select="msxsl:node-set($nodes)/configSections">
				<xsl:if test="count($input/configSections)=0">
					<xsl:apply-templates select="." mode="Copy"/>
				</xsl:if>
			</xsl:for-each>
			<xsl:for-each select="node()">
				<xsl:variable name="name" select="name()" />
				<xsl:choose>
					<xsl:when test="string(@configSource)=''">
						<xsl:apply-templates select="." mode="Prepare">
							<xsl:with-param name="nodes" select="msxsl:node-set($nodes)/*[name()=$name]" />
						</xsl:apply-templates>
					</xsl:when>
					<xsl:otherwise>
						<xsl:apply-templates select="." mode="Copy"/>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:for-each>
			<xsl:for-each select="msxsl:node-set($nodes)/*[name()!='configSections']">
				<xsl:variable name="name" select="name()" />
				<xsl:if test="count($input/*[name()=$name])=0">
					<xsl:choose>
						<xsl:when test="namespace-uri() = '' ">
							<xsl:apply-templates select="." mode="Copy" />
						</xsl:when>
						<xsl:when test="namespace-uri() = 'http://www.composite.net/add/1.0'">
							<xsl:variable name="localName" select="local-name()" />
							<xsl:variable name="keyName">
								<xsl:choose>
									<xsl:when test="@add:key">
										<xsl:value-of select="@add:key" />
									</xsl:when>
									<xsl:otherwise>name</xsl:otherwise>
								</xsl:choose>
							</xsl:variable>
							<xsl:variable name="keyValue" select="@*[name()=$keyName]" />
							<xsl:if test="count($input/*[local-name()=$localName and @*[name()=$keyName]=$keyValue])=0">
								<xsl:apply-templates mode="Copy" select="." />
							</xsl:if>
						</xsl:when>
					</xsl:choose>
				</xsl:if>
			</xsl:for-each>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="@add:*" mode="Copy"/>

	<xsl:template match="@set:*" mode="Copy">
		<xsl:attribute name="{local-name()}">
			<xsl:value-of select="."/>
		</xsl:attribute>
	</xsl:template>

	<xsl:template match="*" mode="Copy">
		<xsl:element name="{local-name()}">
			<xsl:apply-templates select="@* | node()" mode="Copy"/>
		</xsl:element>
	</xsl:template>

	<xsl:template match="@*" mode="Copy">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()" mode="Copy"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="@* | node()">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
		</xsl:copy>
	</xsl:template>
	<!--End Engine-->


</xsl:stylesheet>