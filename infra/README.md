# Infra — shared Azure services

`main.bicep` provisions the two shared Azure services TourList depends on, using **managed identity / RBAC (no keys)**:

| Service | Purpose | Used by |
|---|---|---|
| **Azure Maps** (Gen2) | map tiles + server-side geocoding | M2 browse map, M1 geocoding |
| **Azure Communication Services** + **Email** (Azure-managed domain) | transactional email | M5 password recovery |

## Provisioned dev environment

Bootstrapped in subscription **Pay As You Go** (`b98f55c8-…`), resource group **`rg-tourlist-dev`**, region **Australia East** (ACS data location `Australia`; Azure Maps is global):

- Azure Maps `tourlist-maps-dev` — **clientId `179f22be-4ac0-4135-92ff-b01cb074c246`**
- ACS `tourlist-acs-dev` — endpoint `https://tourlist-acs-dev.australia.communication.azure.com`
- Email `tourlist-email-dev` + `AzureManagedDomain` — sender `DoNotReply@822dbec8-35eb-4521-8329-815a5508aa54.azurecomm.net`
- The signed-in dev user holds **Azure Maps Data Reader** on the Maps account, so the server's `DefaultAzureCredential` (local `az login`) can mint atlas tokens.

## (Re)provision

```bash
az group create -n rg-tourlist-dev -l australiaeast
OBJ=$(az ad signed-in-user show --query id -o tsv)
az deployment group create -g rg-tourlist-dev \
  --template-file infra/main.bicep \
  --parameters mapsReaderPrincipalId=$OBJ
```

The Bicep does the Maps role assignment declaratively (avoids the `az role assignment create` "MissingSubscription" CLI quirk). Read `mapsClientId`, `acsEndpoint`, `senderAddress` from the deployment outputs.

## Local dev wiring (user-secrets, `TourList.Server`)

```bash
dotnet user-secrets set "AzureMaps:Enabled" "true"          --project src/TourList.Server
dotnet user-secrets set "AzureMaps:ClientId" "<mapsClientId>" --project src/TourList.Server
dotnet user-secrets set "AcsEmail:Endpoint" "<acsEndpoint>"  --project src/TourList.Server
dotnet user-secrets set "AcsEmail:SenderAddress" "<sender>"  --project src/TourList.Server
```

With Azure Maps enabled, entries geocode for real and the browse map renders live tiles + pins.

## M7

At deploy time these are folded into the Aspire/`azd` model; the App Service managed identity replaces the dev user for the Maps Data Reader role and any ACS access, and outputs flow to Key Vault / App Service settings (no secrets in source).
