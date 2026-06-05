# LegacyWebApp IIS Deployment Checklist

Quick reference checklist for deploying LegacyWebApp to Windows Server 2022 / IIS.

## Pre-Deployment (Development Machine)

### Build and Package
- [ ] Open PowerShell/Terminal
- [ ] Navigate to LegacyWebApp directory
- [ ] Restore NuGet packages (if needed):
  ```bash
  nuget restore packages.config -PackagesDirectory ../packages
  ```
- [ ] Build in Release mode:
  ```bash
  # Using MSBuild (Windows)
  msbuild LegacyWebApp.csproj /p:Configuration=Release /p:DeployOnBuild=true /p:PublishProfile=FolderProfile
  
  # Or using Visual Studio: Right-click project > Publish > FolderProfile
  ```
- [ ] Verify build output in `bin\Release\Publish`
- [ ] Package for transfer:
  ```bash
  cd bin/Release/Publish
  tar -czf legacywebapp.tar.gz *
  # Or zip on Windows: Compress-Archive -Path * -DestinationPath legacywebapp.zip
  ```

### Review Configuration
- [ ] Review `Web.config` settings
- [ ] Update connection strings for production SQL Server
- [ ] Update appSettings values if needed
- [ ] Set `<customErrors mode="RemoteOnly">` for production
- [ ] Set `<compilation debug="false">` for production

## Windows Server 2022 VM Setup

### Initial Server Configuration
- [ ] Connect to Windows Server 2022 VM (RDP/Console)
- [ ] Verify Windows updates are applied
- [ ] Set appropriate hostname
- [ ] Configure static IP (if required)
- [ ] Join domain (if applicable)

### Install Prerequisites
- [ ] Open PowerShell as Administrator
- [ ] Install IIS with ASP.NET:
  ```powershell
  Install-WindowsFeature -name Web-Server -IncludeManagementTools
  Install-WindowsFeature -name Web-Asp-Net45
  Install-WindowsFeature -name Web-Net-Ext45
  Install-WindowsFeature -name Web-ISAPI-Ext
  Install-WindowsFeature -name Web-ISAPI-Filter
  ```
- [ ] Verify .NET Framework 4.8 is installed:
  ```powershell
  Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" | Select-Object Version, Release
  ```
- [ ] Install URL Rewrite Module (optional, for HTTPS redirect):
  - Download from: https://www.iis.net/downloads/microsoft/url-rewrite

### SQL Server Configuration (if needed)
- [ ] Install SQL Server or configure remote SQL Server access
- [ ] Create database: `CustomerDB`
- [ ] Create SQL Server login/user for IIS Application Pool:
  ```sql
  USE CustomerDB
  CREATE LOGIN [IIS AppPool\LegacyWebAppPool] FROM WINDOWS
  CREATE USER [IIS AppPool\LegacyWebAppPool] FOR LOGIN [IIS AppPool\LegacyWebAppPool]
  EXEC sp_addrolemember 'db_datareader', 'IIS AppPool\LegacyWebAppPool'
  EXEC sp_addrolemember 'db_datawriter', 'IIS AppPool\LegacyWebAppPool'
  ```
- [ ] Test SQL Server connectivity

## Application Deployment

### Transfer Files
- [ ] Create directory: `C:\inetpub\LegacyWebApp`
- [ ] Transfer `legacywebapp.tar.gz` or `legacywebapp.zip` to server
  ```powershell
  # Via SCP (from dev machine)
  scp legacywebapp.tar.gz administrator@SERVER_IP:C:\temp\
  
  # Or use WinSCP, SFTP, shared folder, etc.
  ```
- [ ] Extract files to `C:\inetpub\LegacyWebApp`:
  ```powershell
  cd C:\inetpub
  tar -xzf C:\temp\legacywebapp.tar.gz -C LegacyWebApp
  # Or: Expand-Archive -Path C:\temp\legacywebapp.zip -DestinationPath LegacyWebApp
  ```

### Configure Web.config
- [ ] Open `C:\inetpub\LegacyWebApp\Web.config`
- [ ] Update connection string:
  ```xml
  <connectionStrings>
    <add name="DefaultConnection"
         connectionString="Data Source=SQL_SERVER_NAME;Initial Catalog=CustomerDB;Integrated Security=True"
         providerName="System.Data.SqlClient"/>
  </connectionStrings>
  ```
- [ ] Update appSettings values if needed
- [ ] Verify `<customErrors mode="RemoteOnly">`
- [ ] Verify `<compilation debug="false">`

## IIS Configuration

### Automated Deployment (Recommended)
- [ ] Copy `deployment-script.ps1` to server
- [ ] Open PowerShell as Administrator
- [ ] Run deployment script:
  ```powershell
  # For standalone website
  .\deployment-script.ps1 -AppPath "C:\inetpub\LegacyWebApp" -SiteName "LegacyWebApp" -Port 80
  
  # Or as application under Default Web Site
  .\deployment-script.ps1 -AppPath "C:\inetpub\LegacyWebApp" -SiteName "LegacyWebApp" -AsApplication
  ```
- [ ] Review script output for errors

### Manual Deployment (Alternative)
If not using the automated script:

- [ ] Create Application Pool:
  ```powershell
  Import-Module WebAdministration
  New-WebAppPool -Name "LegacyWebAppPool"
  Set-ItemProperty IIS:\AppPools\LegacyWebAppPool -Name "managedRuntimeVersion" -Value "v4.0"
  Set-ItemProperty IIS:\AppPools\LegacyWebAppPool -Name "managedPipelineMode" -Value "Integrated"
  ```

- [ ] Create Website:
  ```powershell
  New-Website -Name "LegacyWebApp" -Port 80 -PhysicalPath "C:\inetpub\LegacyWebApp" -ApplicationPool "LegacyWebAppPool"
  ```

- [ ] Set Permissions:
  ```powershell
  $acl = Get-Acl "C:\inetpub\LegacyWebApp"
  $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
      "IIS AppPool\LegacyWebAppPool",
      "ReadAndExecute",
      "ContainerInherit,ObjectInherit",
      "None",
      "Allow"
  )
  $acl.SetAccessRule($rule)
  Set-Acl "C:\inetpub\LegacyWebApp" $acl
  ```

- [ ] Configure Firewall:
  ```powershell
  New-NetFirewallRule -DisplayName "Allow HTTP" -Direction Inbound -LocalPort 80 -Protocol TCP -Action Allow
  ```

- [ ] Start Website:
  ```powershell
  Start-WebAppPool -Name "LegacyWebAppPool"
  Start-Website -Name "LegacyWebApp"
  ```

## Testing

### Local Testing (on Server)
- [ ] Check Application Pool status:
  ```powershell
  Get-WebAppPoolState -Name "LegacyWebAppPool"
  ```
- [ ] Check Website status:
  ```powershell
  Get-Website -Name "LegacyWebApp"
  ```
- [ ] Test HTTP locally:
  ```powershell
  Invoke-WebRequest -Uri "http://localhost" -UseBasicParsing
  ```
- [ ] Open browser on server: `http://localhost`
- [ ] Verify home page loads
- [ ] Test navigation to `/Customer/List`

### Remote Testing
- [ ] Test from external machine: `http://SERVER_IP`
- [ ] Verify application loads correctly
- [ ] Test all major functionality:
  - [ ] Home page
  - [ ] Customer list
  - [ ] Database connectivity
  - [ ] Any custom features

### Log Review
- [ ] Check Event Viewer for errors:
  ```powershell
  Get-EventLog -LogName Application -Source "ASP.NET*" -Newest 10
  ```
- [ ] Review IIS logs:
  ```powershell
  Get-Content "C:\inetpub\logs\LogFiles\W3SVC1\*.log" -Tail 50
  ```
- [ ] Check for any HTTP 500 errors or exceptions

## Troubleshooting Common Issues

### Issue: HTTP 503 - Service Unavailable
- [ ] Check Application Pool is started:
  ```powershell
  Start-WebAppPool -Name "LegacyWebAppPool"
  ```
- [ ] Check Event Viewer for Application Pool crashes
- [ ] Verify .NET Framework 4.8 is installed

### Issue: HTTP 500.19 - Configuration Error
- [ ] Validate Web.config XML syntax
- [ ] Check that all IIS features are installed
- [ ] Review detailed error in browser (if customErrors=Off)

### Issue: HTTP 403.14 - Directory Browsing Forbidden
- [ ] Verify `Global.asax` exists in application root
- [ ] Verify `bin` folder contains all DLL files
- [ ] Check that ASP.NET is properly registered

### Issue: Database Connection Errors
- [ ] Test SQL Server connectivity from server
- [ ] Verify connection string in Web.config
- [ ] Check Application Pool Identity has database access
- [ ] Review SQL Server error logs

### Issue: Missing DLL Errors
- [ ] Verify all files were extracted from publish package
- [ ] Check that `bin` folder contains all assemblies
- [ ] Verify NuGet packages were included in publish

## Post-Deployment

### Security Hardening
- [ ] Disable detailed errors (set `<customErrors mode="RemoteOnly">`)
- [ ] Disable directory browsing:
  ```powershell
  Set-WebConfigurationProperty -Filter /system.webServer/directoryBrowse -Name enabled -Value $false -PSPath "IIS:\Sites\LegacyWebApp"
  ```
- [ ] Configure HTTPS (if SSL certificate available)
- [ ] Remove server headers
- [ ] Review and restrict CORS settings (if applicable)

### Monitoring Setup
- [ ] Configure IIS logging
- [ ] Set up log rotation
- [ ] Configure Windows Event Log monitoring
- [ ] Set up application performance monitoring (optional)
- [ ] Configure automated backups

### Documentation
- [ ] Document server IP/hostname
- [ ] Document database connection details
- [ ] Document any custom configuration
- [ ] Document firewall rules applied
- [ ] Create runbook for common operations
- [ ] Document backup/restore procedures

### OpenShift Integration (if applicable)
- [ ] Configure OpenShift Route/Service to VM
- [ ] Set up network policies
- [ ] Configure logging integration
- [ ] Set up monitoring integration
- [ ] Document OpenShift-specific configuration

## Validation Checklist

Before declaring deployment complete:

- [ ] Application loads without errors
- [ ] All pages accessible
- [ ] Database connections working
- [ ] No errors in Event Viewer
- [ ] No errors in IIS logs
- [ ] Appropriate security settings applied
- [ ] Firewall rules configured correctly
- [ ] Performance acceptable
- [ ] Monitoring configured
- [ ] Documentation complete

## Rollback Plan

In case of deployment issues:

- [ ] Stop IIS website:
  ```powershell
  Stop-Website -Name "LegacyWebApp"
  ```
- [ ] Restore previous application files (if updating existing deployment)
- [ ] Restore Web.config from backup
- [ ] Restart website:
  ```powershell
  Start-Website -Name "LegacyWebApp"
  ```
- [ ] Verify rollback successful

## Support Resources

- **IIS Logs**: `C:\inetpub\logs\LogFiles\W3SVC1\`
- **Event Viewer**: Application log, ASP.NET events
- **Full Deployment Guide**: See `IIS-DEPLOYMENT-GUIDE.md`
- **Application Source**: https://github.com/roller1187/mta-dotnet-demo/
