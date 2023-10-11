@description('The suffix applied to deployed resources')
param suffix string = uniqueString(resourceGroup().id)

@description('The name of the Azure Cognitive Search service that will be deployed')
param searchServiceName string = 'search-${suffix}-dev'

@description('The pricing tier of the search service that will be deployed. Default is free')
param searchSku string = 'basic'

@description('The location of the Azure Cognitive Search service that will be deployed. Default is location of the resource group')
param location string = resourceGroup().location

@description('Replicas distribute search workloads across the service.')
param replicaCount int = 1

@description('Partitions allow for scaling of document count as well as faster indexing by sharding your index over multiple search units')
param partitionCount int = 1

@description('The name of the Azure Storage account that will be deployed')
param storageAccountName string = 'storage${suffix}'

@description('The name of the blob container that will be deployed')
param blobContainerName string = 'cog-search-demo'

@description('The SKU of the Azure Storage account that will be deployed. Default is Standard_LRS')
param storageAccountSku string = 'Standard_LRS'

resource searchService 'Microsoft.Search/searchServices@2022-09-01' = {
  name: searchServiceName
  location: location
  sku: {
    name: searchSku
  }
  properties: {
    replicaCount: replicaCount
    partitionCount: partitionCount
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: storageAccountSku
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
  }
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccount.name}/default/${blobContainerName}'
}
