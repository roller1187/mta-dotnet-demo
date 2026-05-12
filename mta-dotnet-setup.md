# Enabling .NET Analysis in MTA

## Option 1: Enable via MTA UI (if available)

1. **Check for Language Providers:**
   - In MTA UI, go to **Administration** → **Repositories** or **Analyzers**
   - Look for **.NET** or **dotnet-external-provider**
   - If not present, you may need to add it manually

2. **Check Feature Flags:**
   - Some MTA versions have tech preview features behind feature flags
   - Check the MTA deployment ConfigMap or settings

## Option 2: Install .NET Analyzer Provider

MTA uses external providers for different language analysis. For .NET, you need the `dotnet-external-provider`.

### Using the CLI to add the provider:

If your MTA cluster is accessible via CLI, you may need to configure the analyzer:

```bash
# Get your MTA hub pod
oc get pods -n <your-mta-namespace>

# Check if dotnet provider is installed
oc exec -n <namespace> <hub-pod-name> -- ls /opt/providers/

# You should see: dotnet-external-provider (or similar)
```

## Option 3: Manual Ruleset Installation

If the provider isn't available, you may need to install .NET rulesets:

1. Download .NET migration rulesets from:
   - https://github.com/konveyor/rulesets
   - Look for `dotnet` or `aspnet` rulesets

2. In MTA UI:
   - Go to **Administration** → **Custom Rules**
   - Upload the .NET ruleset XML files

## Option 4: Use Windup CLI for .NET (Alternative)

If MTA UI doesn't support .NET yet, you can use the Windup CLI directly:

```bash
# Download windup-cli with .NET support
wget https://repo1.maven.org/maven2/org/jboss/windup/windup-cli/<version>/windup-cli-<version>-offline.zip

# Run analysis
./bin/windup-cli --input /path/to/LegacyWebApp \
  --output /path/to/report \
  --source dotnet \
  --target dotnetcore \
  --target linux
```

## Option 5: Check MTA Version

.NET support was added relatively recently. Check your MTA version:
- MTA 6.x+ should have better .NET support
- Earlier versions may not include the dotnet-external-provider

## Current Workaround for Demo

If .NET analysis isn't available yet, consider:
1. **Manual code analysis** - I can help create a detailed migration assessment document
2. **Use alternative tools:**
   - Microsoft's .NET Upgrade Assistant
   - Visual Studio's migration tools
3. **Create a mock MTA report** for demo purposes showing what it would find
