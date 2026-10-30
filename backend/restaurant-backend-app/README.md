# Restaurant App

You can find Web API documentation [here](docs/ApiRoutes.md).

## Run the API locally with Swagger

From PowerShell, run:

```powershell
.\run-local-swagger.ps1
```

The script loads the `api-handler:dev` Lambda environment from AWS, starts the
same API Gateway route handler locally at `http://127.0.0.1:5187`, and opens
Swagger UI. In Swagger, use **Authorize** with a Cognito ID token from `/signin`.
Opaque guest-session bearer tokens are passed through to the existing guest
authorization handlers.

Local API requests use the configured AWS development resources and can read or
modify development data. AWS CLI credentials must have permission to read the
Lambda configuration; AWS SDK credentials must have the service permissions
needed by the operation being tested. The local host binds only to loopback and
does not deploy or change API Gateway.