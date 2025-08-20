using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using System.IO.Compression;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer;

public class DataspecerConnector(
	ILogger<DataspecerConnector> logger) : IDataspecerConnector
{
	//private const string DATASPECER_JSONLD_ENDPOINT = "https://tool.dataspecer.com/api/preview/context.jsonld?iri=";
	private const string DATASPECER_DOWNLOAD_DOCUMENTATION_ENDPOINT = "https://tool.dataspecer.com/api/experimental/output.zip?iri=";

	private readonly HttpClient _httpClient = new HttpClient();
	private readonly ILogger<DataspecerConnector> _logger = logger;

	public async Task<string?> ExportDsvFileFromPackageAsync(string packageIri)
	{
		const string dsvPath = "en/dsv.ttl";
		return await ExportFileFromPackage(packageIri, dsvPath);
	}

	public async Task<string?> ExportOwlFileFromPackageAsync(string packageIri)
	{
		const string dsvPath = "en/model.owl.ttl";
		return await ExportFileFromPackage(packageIri, dsvPath);
	}

	private async Task<string?> ExportFileFromPackage(string packageIri, string filePath)
	{
		string uri = DATASPECER_DOWNLOAD_DOCUMENTATION_ENDPOINT + packageIri;
		HttpResponseMessage response = await _httpClient.GetAsync(uri);
		if (!response.IsSuccessStatusCode)
		{
			string body = await response.Content.ReadAsStringAsync();
			return null;
		}
		byte[] data = await response.Content.ReadAsByteArrayAsync();

		using (MemoryStream zipStream = new MemoryStream(data))
		using (ZipArchive zip = new ZipArchive(zipStream))
		{
			ZipArchiveEntry? file = zip.GetEntry(filePath);
			if (file is null)
			{
				return null;
			}

			using (StreamReader reader = new StreamReader(file.Open()))
			{
				string fileContent = reader.ReadToEnd();
				return fileContent;
			}
		}
	}
}
