deploy-ts:
	cd infrastructure; cd typescript; cdk deploy --all

deploy-net7:
	cd infrastructure; cd net7; cdk deploy --all

build-frontend:
	cd UI;ng build