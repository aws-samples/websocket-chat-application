deploy:
	cd infrastructure; cdk deploy --all

build-frontend:
	cd UI;ng build