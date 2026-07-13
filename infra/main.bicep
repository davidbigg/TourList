// TourList shared Azure services: Azure Maps + Communication Services (Email).
// Bootstrapped for local dev via the Azure CLI; this Bicep is the canonical, reproducible IaC
// (deploy with: az deployment group create -g <rg> --template-file infra/main.bicep).
// At M7 these are folded into the Aspire/azd deployment model.
//
// Auth is managed-identity / RBAC — no keys checked in. Locally the developer's `az login`
// identity is granted Azure Maps Data Reader; in Azure the App Service managed identity is used.

targetScope = 'resourceGroup'

@description('Resource name prefix.')
param namePrefix string = 'tourlist'

@description('Suffix distinguishing environments (e.g. dev, prod).')
param envSuffix string = 'dev'

@description('ACS email data residency location (e.g. "Australia", "United States", "Europe").')
param acsDataLocation string = 'Australia'

@description('Principal (dev user objectId or App Service MI) granted Azure Maps Data Reader. Empty skips the assignment.')
param mapsReaderPrincipalId string = ''

@allowed([ 'User', 'ServicePrincipal' ])
param mapsReaderPrincipalType string = 'User'

// Azure Maps Data Reader — least-privilege read access to Maps data-plane (tiles + geocoding).
var mapsDataReaderRoleId = '423170ca-a8f6-4b0f-8487-9e4eb8f49bfa'

resource maps 'Microsoft.Maps/accounts@2023-06-01' = {
  name: '${namePrefix}-maps-${envSuffix}'
  location: 'global'
  sku: { name: 'G2' } // Gen2 pricing
  kind: 'Gen2'
}

resource mapsReaderAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(mapsReaderPrincipalId)) {
  name: guid(maps.id, mapsReaderPrincipalId, mapsDataReaderRoleId)
  scope: maps
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', mapsDataReaderRoleId)
    principalId: mapsReaderPrincipalId
    principalType: mapsReaderPrincipalType
  }
}

resource email 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: '${namePrefix}-email-${envSuffix}'
  location: 'global'
  properties: {
    dataLocation: acsDataLocation
  }
}

// Free Azure-managed sender subdomain (auto-verified; no custom DNS needed for dev).
resource emailDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: email
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

resource acs 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: '${namePrefix}-acs-${envSuffix}'
  location: 'global'
  properties: {
    dataLocation: acsDataLocation
    linkedDomains: [ emailDomain.id ]
  }
}

@description('Azure Maps account client id (x-ms-client-id) -> AzureMaps:ClientId.')
output mapsClientId string = maps.properties.uniqueId

@description('ACS endpoint -> AcsEmail:Endpoint.')
output acsEndpoint string = 'https://${acs.properties.hostName}'

@description('Managed sender address -> AcsEmail:SenderAddress.')
output senderAddress string = 'DoNotReply@${emailDomain.properties.fromSenderDomain}'
