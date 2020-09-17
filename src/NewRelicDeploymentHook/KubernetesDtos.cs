using System.Collections.Generic;
using System.Text.Json;

namespace NewRelicDeploymentHook.k8s
{
	public class AdmissionReviewDto
	{
		public string? Kind { get; set; }
		public string? ApiVersion { get; set; }
		public AdmissionRequestDto? Request { get; set; }
		public AdmissionResponseDto? Response { get; set; }
	}

	public class KindDto
	{
		public string? Group { get; set; }
		public string? Version { get; set; }
		public string? Kind { get; set; }
	}

	public class ResourceDto
	{
		public string? Group { get; set; }
		public string? Version { get; set; }
		public string? Resource { get; set; }
	}

	public class UserInfoDto
	{
		public string? Username { get; set; }
		public string? Uid { get; set; }
		public List<string>? Groups { get; set; }
	}

	// AdmissionRequest describes the admission.Attributes for the admission request.
	public class AdmissionRequestDto
	{
		// UID is an identifier for the individual request/response. It allows us to distinguish instances of requests which are
		// otherwise identical (parallel requests, requests when earlier requests did not modify etc)
		// The UID is meant to track the round trip (request/response) between the KAS and the WebHook, not the user request.
		// It is suitable for correlating log entries between the webhook and apiserver, for either auditing or debugging.
		public string? Uid { get; set; }
		// Kind is the type of object being manipulated.  For example: Pod
		public KindDto? Kind { get; set; }
		// Resource is the name of the resource being requested.  This is not the kind.  For example: pods
		public ResourceDto? Resource { get; set; }
		// SubResource is the name of the subresource being requested.  This is a different resource, scoped to the parent
		// resource, but it may have a different kind. For instance, /pods has the resource "pods" and the kind "Pod", while
		// /pods/foo/status has the resource "pods", the sub resource "status", and the kind "Pod" (because status operates on
		// pods). The binding resource for a pod though may be /pods/foo/binding, which has resource "pods", subresource
		// "binding", and kind "Binding".
		// +optional
		public string? SubResource { get; set; }
		// Name is the name of the object as presented in the request.  On a CREATE operation, the client may omit name and
		// rely on the server to generate the name.  If that is the case, this method will return the empty string.
		// +optional
		public string? Name { get; set; }
		// Namespace is the namespace associated with the request (if any).
		// +optional
		public string? Namespace { get; set; }
		// Operation is the operation being performed
		public string? Operation { get; set; }
		// UserInfo is information about the requesting user
		public UserInfoDto? UserInfo { get; set; }
		// Object is the object from the incoming request prior to default values being applied
		// +optional
		public JsonElement? Object { get; set; }
		// OldObject is the existing object. Only populated for UPDATE requests.
		// +optional
		public JsonElement? OldObject { get; set; }
	}

	// AdmissionResponse describes an admission response.
	public class AdmissionResponseDto
	{
		// UID is an identifier for the individual request/response.
		// This should be copied over from the corresponding AdmissionRequest.
		public string? Uid { get; set; }
		// Allowed indicates whether or not the admission request was permitted.
		public bool? Allowed { get; set; }
		// Result contains extra details into why an admission request was denied.
		// This field IS NOT consulted in any way if "Allowed" is "true".
		// +optional
		public JsonElement? Result { get; set; }
		// Patch contains the actual patch. Currently we only support a response in the form of JSONPatch, RFC 6902.
		// +optional
		public byte[]? Patch { get; set; }
		// PatchType indicates the form the Patch will take. Currently we only support "JSONPatch".
		// +optional
		public string? PatchType { get; set; }
	}

	// PatchType is the type of patch being used to represent the mutated object
	// PatchType constants.
	public static class PatchTypes
	{
		public const string JsonPatchPatchType = "JSONPatch";
	}

	// Operation is the type of resource operation being checked for admission control
	// Operation constants
	public static class Opeartions
	{
		public const string CreateOperation = "CREATE";
		public const string UpdateOperation = "UPDATE";
		public const string DeleteOperation = "DELETE";
		public const string ConnectOperation = "CONNECT";
	}
}
