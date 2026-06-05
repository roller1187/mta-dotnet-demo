# LegacyWebApp IIS Deployment Script
# For Windows Server 2022
# Run as Administrator

param(
    [string]$AppPath = "C:\inetpub\LegacyWebApp",
    [string]$SiteName = "LegacyWebApp",
    [string]$AppPoolName = "LegacyWebAppPool",
    [int]$Port = 80,
    [string]$HostName = "",
    [switch]$CreateNewSite = $false,
    [switch]$AsApplication = $false,
    [string]$ParentSite = "Default Web Site"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "LegacyWebApp IIS Deployment Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERROR: This script must be run as Administrator" -ForegroundColor Red
    exit 1
}

# Import IIS module
Write-Host "Importing IIS module..." -ForegroundColor Yellow
Import-Module WebAdministration -ErrorAction Stop

# Create application directory
Write-Host "Creating application directory: $AppPath" -ForegroundColor Yellow
if (!(Test-Path $AppPath)) {
    New-Item -ItemType Directory -Force -Path $AppPath | Out-Null
    Write-Host "  [OK] Directory created" -ForegroundColor Green
} else {
    Write-Host "  [OK] Directory already exists" -ForegroundColor Green
}

# Create Application Pool
Write-Host "Configuring Application Pool: $AppPoolName" -ForegroundColor Yellow
if (!(Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-Host "  [OK] Application Pool created" -ForegroundColor Green
} else {
    Write-Host "  [OK] Application Pool already exists" -ForegroundColor Green
}

# Configure Application Pool settings
Set-ItemProperty IIS:\AppPools\$AppPoolName -Name "managedRuntimeVersion" -Value "v4.0"
Set-ItemProperty IIS:\AppPools\$AppPoolName -Name "managedPipelineMode" -Value "Integrated"
Set-ItemProperty IIS:\AppPools\$AppPoolName -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
Set-ItemProperty IIS:\AppPools\$AppPoolName -Name "enable32BitAppOnWin64" -Value $false
Write-Host "  [OK] Application Pool configured (.NET 4.0, Integrated Pipeline)" -ForegroundColor Green

# Create Website or Application
if ($AsApplication) {
    Write-Host "Creating IIS Application: $SiteName under $ParentSite" -ForegroundColor Yellow
    if (Test-Path "IIS:\Sites\$ParentSite\$SiteName") {
        Write-Host "  [OK] Application already exists, updating configuration" -ForegroundColor Green
        Set-ItemProperty "IIS:\Sites\$ParentSite\$SiteName" -Name physicalPath -Value $AppPath
        Set-ItemProperty "IIS:\Sites\$ParentSite\$SiteName" -Name applicationPool -Value $AppPoolName
    } else {
        New-WebApplication -Name $SiteName -Site $ParentSite -PhysicalPath $AppPath -ApplicationPool $AppPoolName | Out-Null
        Write-Host "  [OK] Application created" -ForegroundColor Green
    }
} else {
    Write-Host "Creating IIS Website: $SiteName" -ForegroundColor Yellow
    if (!(Test-Path "IIS:\Sites\$SiteName")) {
        New-Website -Name $SiteName -Port $Port -PhysicalPath $AppPath -ApplicationPool $AppPoolName | Out-Null
        Write-Host "  [OK] Website created" -ForegroundColor Green
    } else {
        Write-Host "  [OK] Website already exists, updating configuration" -ForegroundColor Green
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $AppPath
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
    }

    # Add host binding if specified
    if ($HostName) {
        Write-Host "Configuring host binding: $HostName" -ForegroundColor Yellow
        $binding = Get-WebBinding -Name $SiteName -Protocol "http" -HostHeader $HostName
        if (!$binding) {
            New-WebBinding -Name $SiteName -IPAddress "*" -Port $Port -Protocol http -HostHeader $HostName | Out-Null
            Write-Host "  [OK] Host binding added" -ForegroundColor Green
        } else {
            Write-Host "  [OK] Host binding already exists" -ForegroundColor Green
        }
    }
}

# Set Permissions
Write-Host "Configuring file system permissions..." -ForegroundColor Yellow
$acl = Get-Acl $AppPath

# Grant IIS_IUSRS read permissions
$iisUsersRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS_IUSRS",
    "ReadAndExecute",
    "ContainerInherit,ObjectInherit",
    "None",
    "Allow"
)
$acl.SetAccessRule($iisUsersRule)

# Grant Application Pool Identity read/execute permissions
$appPoolSid = "IIS AppPool\$AppPoolName"
$appPoolRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $appPoolSid,
    "ReadAndExecute",
    "ContainerInherit,ObjectInherit",
    "None",
    "Allow"
)
$acl.SetAccessRule($appPoolRule)
Set-Acl $AppPath $acl
Write-Host "  [OK] Read permissions granted to IIS_IUSRS and $AppPoolName" -ForegroundColor Green

# Grant write permissions to App_Data if it exists
$appDataPath = Join-Path $AppPath "App_Data"
if (Test-Path $appDataPath) {
    Write-Host "Configuring App_Data write permissions..." -ForegroundColor Yellow
    $dataAcl = Get-Acl $appDataPath
    $writeRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $appPoolSid,
        "Modify",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $dataAcl.SetAccessRule($writeRule)
    Set-Acl $appDataPath $dataAcl
    Write-Host "  [OK] Write permissions granted to App_Data" -ForegroundColor Green
}

# Configure Firewall
if (!$AsApplication) {
    Write-Host "Configuring Windows Firewall..." -ForegroundColor Yellow
    $ruleName = "Allow HTTP for $SiteName"
    $existingRule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    if (!$existingRule) {
        New-NetFirewallRule -DisplayName $ruleName `
            -Direction Inbound `
            -LocalPort $Port `
            -Protocol TCP `
            -Action Allow `
            -ErrorAction SilentlyContinue | Out-Null
        Write-Host "  [OK] Firewall rule created for port $Port" -ForegroundColor Green
    } else {
        Write-Host "  [OK] Firewall rule already exists" -ForegroundColor Green
    }
}

# Start Application Pool and Website
Write-Host "Starting services..." -ForegroundColor Yellow
Start-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
if (!$AsApplication) {
    Start-Website -Name $SiteName -ErrorAction SilentlyContinue
    Write-Host "  [OK] Website started" -ForegroundColor Green
} else {
    Write-Host "  [OK] Application pool started" -ForegroundColor Green
}

# Verify deployment
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Status" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Check Application Pool status
$poolState = Get-WebAppPoolState -Name $AppPoolName
Write-Host "Application Pool Status: $($poolState.Value)" -ForegroundColor $(if ($poolState.Value -eq "Started") { "Green" } else { "Red" })

# Check Website/Application status
if (!$AsApplication) {
    $siteState = Get-Website -Name $SiteName
    Write-Host "Website Status: $($siteState.State)" -ForegroundColor $(if ($siteState.State -eq "Started") { "Green" } else { "Red" })
    Write-Host "Physical Path: $($siteState.PhysicalPath)" -ForegroundColor White
    Write-Host ""
    Write-Host "Access URLs:" -ForegroundColor Cyan
    Write-Host "  http://localhost:$Port" -ForegroundColor White
    if ($HostName) {
        Write-Host "  http://$HostName`:$Port" -ForegroundColor White
    }
    $ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.IPAddress -ne "127.0.0.1" -and $_.PrefixOrigin -eq "Dhcp" -or $_.PrefixOrigin -eq "Manual"} | Select-Object -First 1).IPAddress
    if ($ip) {
        Write-Host "  http://$ip`:$Port" -ForegroundColor White
    }
} else {
    Write-Host "Application: /$SiteName under $ParentSite" -ForegroundColor White
    Write-Host "Physical Path: $AppPath" -ForegroundColor White
    Write-Host ""
    Write-Host "Access URLs:" -ForegroundColor Cyan
    $parentSite = Get-Website -Name $ParentSite
    $parentBinding = $parentSite.bindings.Collection[0]
    $parentPort = $parentBinding.bindingInformation.Split(':')[1]
    Write-Host "  http://localhost:$parentPort/$SiteName" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "1. Copy application files to: $AppPath" -ForegroundColor Yellow
Write-Host "2. Update Web.config connection strings if needed" -ForegroundColor Yellow
Write-Host "3. Test the application in a browser" -ForegroundColor Yellow
Write-Host "4. Check Event Viewer for any errors" -ForegroundColor Yellow
Write-Host "5. Review IIS logs: C:\inetpub\logs\LogFiles" -ForegroundColor Yellow
Write-Host ""
Write-Host "Deployment completed successfully!" -ForegroundColor Green
