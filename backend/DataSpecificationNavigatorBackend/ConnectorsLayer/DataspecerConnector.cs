using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using System.IO.Compression;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer;

public class DataspecerConnector : IDataspecerConnector
{
	private readonly HttpClient _httpClient;
	private readonly ILogger<DataspecerConnector> _logger;
	private readonly string _dataspecerDownloadDocumentationEndpoint;

	public DataspecerConnector(
	ILogger<DataspecerConnector> logger,
	IConfiguration config)
	{
		_httpClient = new HttpClient();
		_logger = logger;
		string? downloadDocsEndpoint = config["Env:Dataspecer:Endpoints:DownloadDocumentation"];
		if (string.IsNullOrWhiteSpace(downloadDocsEndpoint))
		{
			downloadDocsEndpoint = config["Dataspecer:Endpoints:DownloadDocumentation"];
			if (string.IsNullOrWhiteSpace(downloadDocsEndpoint))
			{
				throw new Exception("The dataspecer URL is missing from configuration.");
			}
		}
		_dataspecerDownloadDocumentationEndpoint = downloadDocsEndpoint;
	}

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
		string uri = _dataspecerDownloadDocumentationEndpoint + packageIri;
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
