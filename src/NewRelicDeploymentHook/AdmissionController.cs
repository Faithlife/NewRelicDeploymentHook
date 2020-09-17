using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NewRelicDeploymentHook.k8s;

namespace NewRelicDeploymentHook
{
	[ApiController]
	[Route("")]
	public class AdmissionController : ControllerBase
	{
		public AdmissionController(ILogger<AdmissionController> logger, IHttpClientFactory httpClientFactory)
		{
			m_logger = logger;
			m_httpClientFactory = httpClientFactory;
		}

		[HttpPost]
		[Route("mutate")]
		public async Task<AdmissionReviewDto> Mutate(AdmissionReviewDto review)
		{
			var request = review.Request;
			if (request is null)
				throw new ArgumentException("request is required");
			m_logger.LogDebug(JsonSerializer.Serialize(review, options: s_indentedCamelCaseOptions));
			review.Response = new AdmissionResponseDto
			{
				Uid = request.Uid,
				Allowed = true,
			};
			if (request.Kind?.Kind == "Deployment")
			{
				var patch = await MutateDeployment(request);
				if (patch != null)
				{
					var patchJson = JsonSerializer.Serialize(patch, s_camelCaseOptions);
					m_logger.LogInformation("Applying patch: {Patch}", patchJson);

					review.Response.PatchType = PatchTypes.JsonPatchPatchType;
					review.Response.Patch = Encoding.UTF8.GetBytes(patchJson);
				}
			}
			return review;
		}

		private async Task<IReadOnlyList<JsonPatchOperation>?> MutateDeployment(AdmissionRequestDto request)
		{
			if (!(request.Object is JsonElement objectEl))
			{
				m_logger.LogWarning("Invalid object");
				return null;
			}

			var metadataEl = objectEl.GetProperty("metadata");
			if (!metadataEl.TryGetProperty("annotations", out var annotationsEl) || !annotationsEl.TryGetProperty("newRelic", out var newRelicEl))
			{
				m_logger.LogInformation("No newRelic metadata");
				return null;
			}

			var newRelicMetadata = JsonSerializer.Deserialize<JsonElement>(newRelicEl.GetString());

			var appId = newRelicMetadata.TryGetStringProperty("appId");
			if (appId is null)
			{
				m_logger.LogInformation("No appId");
				return null;
			}

			var revision = newRelicMetadata.TryGetStringProperty("revision");
			if (revision is null)
			{
				m_logger.LogInformation("No revision");
				return null;
			}

			var deployment = new DeploymentDto
			{
				Revision = revision,
				User = (request.UserInfo?.Username),
				// Omit timestamp. New Relic will respond with BadRequest if the timestamp is in the future by even 1 second.
			};

			var deploymentJson = JsonSerializer.Serialize(deployment, s_camelCaseOptions);

			m_logger.LogInformation("New Relic Deployment: {Deployment}", deploymentJson);

			var lastAppliedDeploymentJson = annotationsEl.TryGetStringProperty("lastAppliedNewRelicDeployment");
			var lastAppliedDeployment = lastAppliedDeploymentJson is null ? default(JsonElement?) : JsonSerializer.Deserialize<JsonElement>(lastAppliedDeploymentJson);
			var lastAppliedRevision = lastAppliedDeployment?.TryGetStringProperty("revision");

			if (lastAppliedRevision == revision)
			{
				m_logger.LogInformation("No change from last deployment");
				return null;
			}

			var deploymentResponse = await RecordDeploymentAsync(appId, deployment);
			var patch = new[]
			{
				new JsonPatchOperation("add", "/metadata/annotations/lastAppliedNewRelicDeployment", JsonSerializer.Serialize(deploymentResponse)),
			};
			return patch;
		}

		private async Task<JsonElement?> RecordDeploymentAsync(string appId, DeploymentDto deployment)
		{
			using var client = m_httpClientFactory.CreateClient("newrelic");
			var response = await client.PostAsJsonAsync($"applications/{appId}/deployments.json", new
			{
				deployment,
			});
			var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>();
			m_logger.LogInformation("Recorded deployment\n{StatusCode} {Reason}: {Content}", (int) response.StatusCode, response.ReasonPhrase, responseContent);
			return response.IsSuccessStatusCode ? responseContent.GetProperty("deployment") : default(JsonElement?);
		}

		private static readonly JsonSerializerOptions s_camelCaseOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		private static readonly JsonSerializerOptions s_indentedCamelCaseOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
		private readonly ILogger<AdmissionController> m_logger;
		private readonly IHttpClientFactory m_httpClientFactory;
	}

	internal static class JsonUtility
	{
		public static string? TryGetStringProperty(this JsonElement element, string propertyName)
			=> element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
	}

	internal class DeploymentDto
	{
		public string? Revision { get; set; }
		public string? Changelog { get; set; }
		public string? Description { get; set; }
		public string? User { get; set; }
		public DateTime? Timestamp { get; set; }
	}

	internal class JsonPatchOperation
	{
		public JsonPatchOperation(string op, string path, string value)
		{
			Op = op;
			Path = path;
			Value = value;
		}

		public string Op { get; }
		public string Path { get; }
		public string Value { get; }

		public override bool Equals(object? obj) => obj is JsonPatchOperation other && Op == other.Op && Path == other.Path && Value == other.Value;
		public override int GetHashCode() => HashCode.Combine(Op, Path, Value);
	}
}
