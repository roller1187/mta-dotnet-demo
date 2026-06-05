# LegacyWebApp IIS Deployment Guide
## Windows Server 2022 Deployment Instructions

This guide covers deploying the LegacyWebApp ASP.NET MVC 5 application to IIS on Windows Server 2022.

## Prerequisites

### 1. Windows Server 2022 VM Requirements
- Windows Server 2022 Standard or Datacenter
- Minimum 2 GB RAM (4 GB recommended)
- 40 GB disk space
- Network connectivity

### 2. Required Software Components

#### Install IIS with ASP.NET Support
Run in PowerShell as Administrator:

```powershell
# Install IIS with required features
Install-WindowsFeature -name Web-Server -IncludeManagementTools
Install-WindowsFeature -name Web-Asp-Net45
Install-WindowsFeature -name Web-Net-Ext45
Install-WindowsFeature -name Web-ISAPI-Ext
Install-WindowsFeature -name Web-ISAPI-Filter

# Verify installation
Get-WindowsFeature -Name Web-* | Where-Object {$_.Installed -eq $True}
```

#### Install .NET Framework 4.8
.NET Framework 4.8 should be pre-installed on Windows Server 2022. Verify:

```powershell
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" | Select-Object Version, Release
```

Expected output: Version 4.8.x, Release >= 528040

If not installed, download from: https://dotnet.microsoft.com/download/dotnet-framework/net48

#### Install MSBuild (Optional - for building on server)
If you plan to build on the server rather than deploying pre-built binaries:

Option 1: Install Visual Studio 2022 (includes MSBuild)
Option 2: Install Build Tools for Visual Studio 2022
- Download from: https://visualstudio.microsoft.com/downloads/
- Select ".NET desktop build tools" workload

## Deployment Methods

### Method 1: Deploy Pre-Built Application (Recommended)

#### Step 1: Build the Application Locally
On your development machine:

```bash
cd ~/mta-dotnet-demo/LegacyWebApp
msbuild LegacyWebApp.csproj /p:Configuration=Release /p:DeployOnBuild=true /p:PublishProfile=FolderProfile
```

Or using Visual Studio:
1. Open LegacyWebApp.sln in Visual Studio
2. Right-click LegacyWebApp project → Publish
3. Choose "Folder" as target
4. Set target location: `bin\Release\Publish`
5. Click "Publish"

#### Step 2: Create Publish Profile (if not exists)

Create `LegacyWebApp/Properties/PublishProfiles/FolderProfile.pubxml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <WebPublishMethod>FileSystem</WebPublishMethod>
    <PublishProvider>FileSystem</PublishProvider>
    <LastUsedBuildConfiguration>Release</LastUsedBuildConfiguration>
    <LastUsedPlatform>Any CPU</LastUsedPlatform>
    <SiteUrlToLaunchAfterPublish />
    <LaunchSiteAfterPublish>True</LaunchSiteAfterPublish>
    <ExcludeApp_Data>False</ExcludeApp_Data>
    <publishUrl>bin\Release\Publish</publishUrl>
    <DeleteExistingFiles>True</DeleteExistingFiles>
  </PropertyGroup>
</Project>
```

#### Step 3: Transfer Files to Windows Server

Package the published files:
```bash
cd ~/mta-dotnet-demo/LegacyWebApp/bin/Release/Publish
tar -czf legacywebapp.tar.gz *
```

Transfer to Windows Server using SCP, SFTP, or shared folder:
```bash
scp legacywebapp.tar.gz administrator@YOUR_SERVER_IP:C:\inetpub\
```

#### Step 4: Extract on Windows Server
On Windows Server (PowerShell):

```powershell
# Create application directory
New-Item -ItemType Directory -Force -Path C:\inetpub\LegacyWebApp

# If using tar.gz, extract using tar (available in Windows Server 2022)
cd C:\inetpub
tar -xzf legacywebapp.tar.gz -C LegacyWebApp

# Or copy files directly if transferred as folder
# Copy-Item -Path \\source\LegacyWebApp\* -Destination C:\inetpub\LegacyWebApp -Recurse
```

### Method 2: Build on Server

#### Step 1: Transfer Source Code
Copy the entire LegacyWebApp folder to the server.

#### Step 2: Restore NuGet Packages
```powershell
cd C:\path\to\LegacyWebApp
nuget restore packages.config -PackagesDirectory ..\packages
```

#### Step 3: Build with MSBuild
```powershell
msbuild LegacyWebApp.csproj /p:Configuration=Release
```

## IIS Configuration

### Step 1: Create Application Pool

```powershell
# Import IIS module
Import-Module WebAdministration

# Create Application Pool
New-WebAppPool -Name "LegacyWebAppPool"

# Configure Application Pool
Set-ItemProperty IIS:\AppPools\LegacyWebAppPool -Name "managedRuntimeVersion" -Value "v4.0"
Set-ItemProperty IIS:\AppPools\LegacyWebAppPool -Name "managedPipelineMode" -Value "Integrated"

# Set identity (ApplicationPoolIdentity is recommended for security)
Set-ItemProperty IIS:\AppPools\LegacyWebAppPool -Name "processModel.identityType" -Value "ApplicationPoolIdentity"

# Optional: Configure recycling (default is 1740 minutes / 29 hours)
Set-ItemProperty IIS:\AppPools\LegacyWebAppPool -Name "recycling.periodicRestart.time" -Value "00:00:00"
```

### Step 2: Create IIS Website

```powershell
# Remove default website (optional)
# Remove-Website -Name "Default Web Site"

# Create new website
New-Website -Name "LegacyWebApp" `
    -Port 80 `
    -PhysicalPath "C:\inetpub\LegacyWebApp" `
    -ApplicationPool "LegacyWebAppPool"

# Configure bindings
# For HTTP
New-WebBinding -Name "LegacyWebApp" -IPAddress "*" -Port 80 -Protocol http

# For HTTPS (if certificate available)
# New-WebBinding -Name "LegacyWebApp" -IPAddress "*" -Port 443 -Protocol https
# $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -eq "CN=yourserver.com"}
# New-Item -Path "IIS:\SslBindings\0.0.0.0!443" -Value $cert
```

Or create as an Application under Default Web Site:

```powershell
New-WebApplication -Name "LegacyWebApp" `
    -Site "Default Web Site" `
    -PhysicalPath "C:\inetpub\LegacyWebApp" `
    -ApplicationPool "LegacyWebAppPool"
```

### Step 3: Set Permissions

```powershell
# Grant IIS_IUSRS read permissions
$acl = Get-Acl "C:\inetpub\LegacyWebApp"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS_IUSRS",
    "ReadAndExecute",
    "ContainerInherit,ObjectInherit",
    "None",
    "Allow"
)
$acl.SetAccessRule($rule)
Set-Acl "C:\inetpub\LegacyWebApp" $acl

# Grant Application Pool Identity permissions
$appPoolSid = "IIS AppPool\LegacyWebAppPool"
$rule2 = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $appPoolSid,
    "ReadAndExecute",
    "ContainerInherit,ObjectInherit",
    "None",
    "Allow"
)
$acl.SetAccessRule($rule2)
Set-Acl "C:\inetpub\LegacyWebApp" $acl

# If App_Data exists, grant write permissions
if (Test-Path "C:\inetpub\LegacyWebApp\App_Data") {
    $dataAcl = Get-Acl "C:\inetpub\LegacyWebApp\App_Data"
    $writeRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $appPoolSid,
        "Modify",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $dataAcl.SetAccessRule($writeRule)
    Set-Acl "C:\inetpub\LegacyWebApp\App_Data" $dataAcl
}
```

### Step 4: Configure Web.config for IIS

Ensure Web.config is properly configured. The existing Web.config should work, but verify:

```xml
<system.webServer>
  <handlers>
    <remove name="ExtensionlessUrlHandler-Integrated-4.0"/>
    <remove name="OPTIONSVerbHandler"/>
    <remove name="TRACEVerbHandler"/>
    <add name="ExtensionlessUrlHandler-Integrated-4.0" 
         path="*." 
         verb="*" 
         type="System.Web.Handlers.TransferRequestHandler" 
         preCondition="integratedMode,runtimeVersionv4.0"/>
  </handlers>
</system.webServer>
```

### Step 5: Configure Database Connection (if applicable)

Edit `C:\inetpub\LegacyWebApp\Web.config`:

```xml
<connectionStrings>
  <add name="DefaultConnection"
       connectionString="Data Source=YOUR_SQL_SERVER;Initial Catalog=CustomerDB;Integrated Security=True"
       providerName="System.Data.SqlClient"/>
</connectionStrings>
```

Or use SQL Server authentication:

```xml
<connectionStrings>
  <add name="DefaultConnection"
       connectionString="Data Source=YOUR_SQL_SERVER;Initial Catalog=CustomerDB;User ID=your_user;Password=your_password"
       providerName="System.Data.SqlClient"/>
</connectionStrings>
```

### Step 6: Configure Firewall

```powershell
# Allow HTTP traffic
New-NetFirewallRule -DisplayName "Allow HTTP" -Direction Inbound -LocalPort 80 -Protocol TCP -Action Allow

# Allow HTTPS traffic (if using)
New-NetFirewallRule -DisplayName "Allow HTTPS" -Direction Inbound -LocalPort 443 -Protocol TCP -Action Allow
```

## Testing the Deployment

### Step 1: Start the Website

```powershell
# Start Application Pool
Start-WebAppPool -Name "LegacyWebAppPool"

# Start Website
Start-Website -Name "LegacyWebApp"

# Verify status
Get-Website -Name "LegacyWebApp"
Get-WebAppPoolState -Name "LegacyWebAppPool"
```

### Step 2: Test Locally on Server

```powershell
# Test with PowerShell
Invoke-WebRequest -Uri "http://localhost/LegacyWebApp" -UseBasicParsing

# Or open in browser
Start-Process "http://localhost/LegacyWebApp"
```

### Step 3: Test from External Machine

```bash
curl http://YOUR_SERVER_IP/LegacyWebApp
```

Or open in browser: `http://YOUR_SERVER_IP/LegacyWebApp`

## Troubleshooting

### Check Application Pool Status

```powershell
Get-WebAppPoolState -Name "LegacyWebAppPool"
Get-EventLog -LogName Application -Source "ASP.NET*" -Newest 10
```

### View IIS Logs

```powershell
# Default log location
Get-Content "C:\inetpub\logs\LogFiles\W3SVC1\*.log" -Tail 50
```

### Common Issues

#### 1. HTTP Error 500.19 - Configuration Error
**Cause:** Web.config syntax error or missing IIS features

**Solution:**
- Verify Web.config XML is valid
- Ensure all required IIS features are installed
- Check Event Viewer for detailed error

#### 2. HTTP Error 503 - Service Unavailable
**Cause:** Application Pool stopped or failed to start

**Solution:**
```powershell
# Check Application Pool state
Get-WebAppPoolState -Name "LegacyWebAppPool"

# Check Event Viewer
Get-EventLog -LogName Application -Newest 10 | Where-Object {$_.Source -like "*ASP.NET*"}

# Restart Application Pool
Restart-WebAppPool -Name "LegacyWebAppPool"
```

#### 3. HTTP Error 403.14 - Directory Browsing Forbidden
**Cause:** Default document not found or MVC routing not configured

**Solution:**
- Verify Global.asax exists
- Check RouteConfig.cs is properly configured
- Ensure bin folder contains all DLLs

#### 4. Database Connection Errors
**Cause:** SQL Server not accessible or connection string incorrect

**Solution:**
- Verify SQL Server is running
- Test connection with sqlcmd:
  ```powershell
  sqlcmd -S YOUR_SQL_SERVER -d CustomerDB -E
  ```
- Grant Application Pool Identity access to database:
  ```sql
  USE CustomerDB
  CREATE LOGIN [IIS AppPool\LegacyWebAppPool] FROM WINDOWS
  CREATE USER [IIS AppPool\LegacyWebAppPool] FOR LOGIN [IIS AppPool\LegacyWebAppPool]
  EXEC sp_addrolemember 'db_datareader', 'IIS AppPool\LegacyWebAppPool'
  EXEC sp_addrolemember 'db_datawriter', 'IIS AppPool\LegacyWebAppPool'
  ```

#### 5. Missing DLLs
**Cause:** NuGet packages not restored or not deployed

**Solution:**
```powershell
cd C:\inetpub\LegacyWebApp
nuget restore packages.config -PackagesDirectory ..\packages
```

### Enable Detailed Errors

For troubleshooting, enable detailed errors in Web.config:

```xml
<system.web>
  <customErrors mode="Off"/>
  <compilation debug="true" targetFramework="4.8"/>
</system.web>
```

**WARNING:** Disable detailed errors in production (set `customErrors mode="RemoteOnly"`)

## Security Hardening (Production)

### 1. Disable Directory Browsing

```powershell
Set-WebConfigurationProperty -Filter /system.webServer/directoryBrowse `
    -Name enabled -Value $false -PSPath "IIS:\Sites\LegacyWebApp"
```

### 2. Remove Server Header

```powershell
Set-WebConfigurationProperty -Filter /system.webServer/security/requestFiltering `
    -Name removeServerHeader -Value $true -PSPath "IIS:\Sites\LegacyWebApp"
```

### 3. Configure HTTPS Redirect

Add to Web.config:

```xml
<system.webServer>
  <rewrite>
    <rules>
      <rule name="HTTP to HTTPS redirect" stopProcessing="true">
        <match url="(.*)" />
        <conditions>
          <add input="{HTTPS}" pattern="off" ignoreCase="true" />
        </conditions>
        <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
      </rule>
    </rules>
  </rewrite>
</system.webServer>
```

### 4. Set Custom Errors (Production)

```xml
<system.web>
  <customErrors mode="RemoteOnly" defaultRedirect="~/Error">
    <error statusCode="404" redirect="~/Error/NotFound"/>
    <error statusCode="500" redirect="~/Error/ServerError"/>
  </customErrors>
</system.web>
```

## Monitoring

### Enable Application Insights (Optional)

Add to Web.config:

```xml
<appSettings>
  <add key="ApplicationInsightsInstrumentationKey" value="YOUR_KEY"/>
</appSettings>
```

### Configure IIS Logging

```powershell
# Enable detailed logging
Set-WebConfigurationProperty -Filter /system.webServer/httpLogging `
    -Name dontLog -Value $false -PSPath "IIS:\Sites\LegacyWebApp"
```

## OpenShift Integration Notes

If you're deploying in an OpenShift environment with a Windows Server 2022 VM:

### 1. Network Configuration
- Ensure the VM can communicate with OpenShift cluster
- Configure appropriate Security Groups/Firewall rules
- Set up Service/Route if exposing externally through OpenShift

### 2. Storage Considerations
- Use persistent volumes if available
- Configure backup strategy for IIS configuration and application data

### 3. Monitoring Integration
- Forward IIS logs to OpenShift logging (FluentD/Loki)
- Integrate with OpenShift monitoring stack

## Automated Deployment Script

Complete PowerShell script for automated deployment:

```powershell
# deployment-script.ps1
param(
    [string]$AppPath = "C:\inetpub\LegacyWebApp",
    [string]$SiteName = "LegacyWebApp",
    [string]$AppPoolName = "LegacyWebAppPool",
    [int]$Port = 80
)

# Import IIS module
Import-Module WebAdministration

# Create directory
New-Item -ItemType Directory -Force -Path $AppPath

# Create Application Pool
if (!(Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName
    Set-ItemProperty IIS:\AppPools\$AppPoolName -Name "managedRuntimeVersion" -Value "v4.0"
    Set-ItemProperty IIS:\AppPools\$AppPoolName -Name "managedPipelineMode" -Value "Integrated"
    Write-Host "Application Pool '$AppPoolName' created successfully"
} else {
    Write-Host "Application Pool '$AppPoolName' already exists"
}

# Create Website
if (!(Test-Path "IIS:\Sites\$SiteName")) {
    New-Website -Name $SiteName -Port $Port -PhysicalPath $AppPath -ApplicationPool $AppPoolName
    Write-Host "Website '$SiteName' created successfully"
} else {
    Write-Host "Website '$SiteName' already exists"
    Set-ItemProperty IIS:\Sites\$SiteName -Name physicalPath -Value $AppPath
    Set-ItemProperty IIS:\Sites\$SiteName -Name applicationPool -Value $AppPoolName
}

# Set Permissions
$acl = Get-Acl $AppPath
$appPoolSid = "IIS AppPool\$AppPoolName"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $appPoolSid,
    "ReadAndExecute",
    "ContainerInherit,ObjectInherit",
    "None",
    "Allow"
)
$acl.SetAccessRule($rule)
Set-Acl $AppPath $acl

# Configure Firewall
New-NetFirewallRule -DisplayName "Allow HTTP for $SiteName" `
    -Direction Inbound -LocalPort $Port -Protocol TCP -Action Allow -ErrorAction SilentlyContinue

# Start Website
Start-Website -Name $SiteName
Start-WebAppPool -Name $AppPoolName

Write-Host "Deployment completed successfully!"
Write-Host "Website available at: http://localhost:$Port"
```

Usage:
```powershell
.\deployment-script.ps1 -AppPath "C:\inetpub\LegacyWebApp" -SiteName "LegacyWebApp" -Port 80
```

## Next Steps

1. Test all application functionality
2. Configure monitoring and logging
3. Set up automated backups
4. Document any custom configuration
5. Create runbook for common operations
6. Plan for migration to .NET 8 (see README.md)
