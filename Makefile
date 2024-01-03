deploy-ts:
	cd infrastructure-ts; cdk deploy --all

deploy-dotnet:
	cd infrastructure-dotnet; cdk deploy --all

build-frontend:
	cd UI;ng build