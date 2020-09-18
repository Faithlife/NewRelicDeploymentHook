# Record Kubernetes deployments to New Relic

## Installation

1. Create the api key secret.
    ```
    kubectl create secret generic newrelic-api-key --from-literal=api-key=<yourapikey>`
    ```
2. Create the certificate for the webhook.
    ```
    k8s/webhook-create-signed-cert.sh
    ```
3. Update the config with your cluster's CA certificate.
    ```
    k8s/webhook-update-ca-bundle.sh
    ```
4. Create the webhook resources.
    ```
    cat k8s/webhook-*.yaml | kubectl apply -f -
    ```
5. Label the namespace you want to enable deploymet recording.
    ```
    kubectl label namespace mynamespace newrelic-deployment=enabled
    ```
6. Add an annotation to your deployment:
    ```
    apiVersion: apps/v1
    kind: Deployment
    metadata:
      name: example-deployment
      annotations:
        newRelic: |
          {"appId":"<your app id>","revision":"<revision>"}
    ```
7. Deploy your app.
8. Check the New Relic dashboard for your deployment.

