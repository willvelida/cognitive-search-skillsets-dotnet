using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Configuration;
using SkillsetsDemo.Models;

IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false)
    .Build();

string searchServiceUri = configuration["SearchServiceUri"];
string adminApiKey = configuration["SearchServiceAdminApiKey"];

SearchIndexClient indexClient = new SearchIndexClient(new Uri(searchServiceUri), new AzureKeyCredential(adminApiKey));
SearchIndexerClient indexerClient = new SearchIndexerClient(new Uri(searchServiceUri), new AzureKeyCredential(adminApiKey));

// Create a Skillset Pipeline

// Step 1: Create a data source
SearchIndexerDataSourceConnection dataSource = new SearchIndexerDataSourceConnection(
    name: "demodata",
    type: SearchIndexerDataSourceType.AzureBlob,
    connectionString: configuration["AzureBlobConnectionString"],
    container: new SearchIndexerDataContainer("cog-search-demo"))
{
    Description = "Demo files to demonstrate cognitive search capabilities"
};

try
{
    await indexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSource);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    throw;
}

// Step 2: Create a skillset
List<InputFieldMappingEntry> inputFieldMappingEntries = new List<InputFieldMappingEntry>();
inputFieldMappingEntries.Add(new InputFieldMappingEntry("text")
{
    Source = "/document/pages/*"
});
inputFieldMappingEntries.Add(new InputFieldMappingEntry("languageCode")
{
    Source = "/document/languageCode"
});

List<OutputFieldMappingEntry> outputFieldMappingEntries = new List<OutputFieldMappingEntry>();
outputFieldMappingEntries.Add(new OutputFieldMappingEntry("keyPhrases")
{
    TargetName = "keyPhrases"
});

KeyPhraseExtractionSkill keyPhraseExtractionSkill = new KeyPhraseExtractionSkill(inputFieldMappingEntries, outputFieldMappingEntries)
{
    Description = "Extract the key phrases",
    Context = "/document/pages/*",
    DefaultLanguageCode = KeyPhraseExtractionSkillLanguage.En
};

List<SearchIndexerSkill> skills = new List<SearchIndexerSkill>();
skills.Add(keyPhraseExtractionSkill);

// Creating a skillset using the built in skillset (not specifying an AI services key)
SearchIndexerSkillset skillset = new SearchIndexerSkillset("demoskillset", skills)
{
    Description = "Demo skillset"
};

try
{
    await indexerClient.CreateOrUpdateSkillsetAsync(skillset);
}
catch (Exception ex)
{
    Console.WriteLine($"Exception thrown: {ex.Message}");
    throw;
}

// Step 3: Create an index
FieldBuilder builder = new FieldBuilder();
var index = new SearchIndex("demoindex")
{
    Fields = builder.Build(typeof(DemoIndex))
};

try
{
    indexClient.GetIndex(index.Name);
    indexClient.DeleteIndex(index.Name);
}
catch (RequestFailedException ex) when (ex.Status == 404)
{
    // throw 404
}

try
{
    indexClient.CreateIndex(index);
}
catch (Exception ex)
{
    Console.WriteLine($"Exception thrown: {ex.Message}");
    throw;
}

// Step 4: Create and run an indexer
IndexingParameters parameters = new IndexingParameters()
{
    MaxFailedItems = -1,
    MaxFailedItemsPerBatch = -1,
    IndexingParametersConfiguration = new IndexingParametersConfiguration()
};
parameters.IndexingParametersConfiguration.Add("dataToExtract", "contentAndMetadata");
parameters.IndexingParametersConfiguration.Add("imageAction", "generateNormalizedImages");

SearchIndexer indexer = new SearchIndexer("demoindexer", dataSource.Name, index.Name)
{
    Description = "Demo indexer",
    SkillsetName = skillset.Name,
    Parameters = parameters
};

FieldMappingFunction mappingFunction = new FieldMappingFunction("base64Encode");
mappingFunction.Parameters.Add("useHttpServerUtilityUrlTokenEncode", true);

indexer.FieldMappings.Add(new FieldMapping("metadata_storage_path")
{
    TargetFieldName = "id",
    MappingFunction = mappingFunction

});
indexer.FieldMappings.Add(new FieldMapping("content")
{
    TargetFieldName = "content"
});

indexer.OutputFieldMappings.Add(new FieldMapping("/document/pages/*/organizations/*")
{
    TargetFieldName = "organizations"
});
indexer.OutputFieldMappings.Add(new FieldMapping("/document/pages/*/keyPhrases/*")
{
    TargetFieldName = "keyPhrases"
});
indexer.OutputFieldMappings.Add(new FieldMapping("/document/languageCode")
{
    TargetFieldName = "languageCode"
});

try
{
    indexerClient.GetIndexer(indexer.Name);
    indexerClient.DeleteIndexer(indexer.Name);
}
catch (RequestFailedException ex) when (ex.Status == 404)
{
    //if the specified indexer not exist, 404 will be thrown.
}

try
{
    indexerClient.CreateIndexer(indexer);
}
catch (RequestFailedException ex)
{
    Console.WriteLine("Failed to create the indexer\n Exception message: {0}\n", ex.Message);
}

// Step 4: Monitor Indexing
try
{
    var demoIndexerExecutionInfo = indexerClient.GetIndexerStatus(indexer.Name);

    switch (demoIndexerExecutionInfo.Value.Status)
    {
        case IndexerStatus.Unknown:
            Console.WriteLine("The status is unknown!");
            break;
        case IndexerStatus.Error:
            Console.WriteLine("There's an error. Check the portal");
            break;
        case IndexerStatus.Running:
            Console.WriteLine("The Indexer is running!");
            break;
        default:
            Console.WriteLine("No information available");
            break;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Exception thrown: {ex.Message}");
    throw;
}