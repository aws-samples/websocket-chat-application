// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { MetricUnits } from '@aws-lambda-powertools/metrics';

// Helper class to send websocket messages to ALL connected users.
export class WebsocketBroadcaster {

    constructor(private AWS: any,
        private metrics: any,
        private dynamoDbClient: any,
        private logger: any,
        private connectionsTableName: string) { }

    private _apiGatewayEndpoint!: string;
    private _apigwManagementApi: any;

    async broadcast(payload: any, apiGatewayEndpoint: string) {
        try {

            this.logger.debug('Retrieving active connections...');
            let connectionData = await this.dynamoDbClient.scan({ TableName: this.connectionsTableName, ProjectionExpression: 'connectionId' }).promise();
            this.logger.debug('ConnectionData:', connectionData);
            this.logger.debug(`Cached ApiGatewayEndpoint: ${this._apiGatewayEndpoint}`);
            this.logger.debug(`New ApiGatewayEndpoint: ${apiGatewayEndpoint}`);

            this._apigwManagementApi = new this.AWS.ApiGatewayManagementApi({ apiVersion: '2018-11-29', endpoint: apiGatewayEndpoint });

            await Promise.all(connectionData.Items.map(async (connectionData: any) => {
                this.logger.debug(`Sending message to ${connectionData.connectionId}`);
                await this._apigwManagementApi.postToConnection({ ConnectionId: connectionData.connectionId, Data: JSON.stringify(payload) }).promise()
                    .then(()=> {
                        this.metrics.addMetric('messageDelivered', MetricUnits.Count, 1);
                        this.logger.debug(`Message sent to connection ${connectionData.connectionId}`);
                    })
                    .catch((err: any) => {
                        this.logger.debug(`Error during message delivery: ${JSON.stringify(err)}`);
                        if (err.statusCode === 410) {
                            this.logger.debug(`Found stale connection, deleting ${connectionData.connectionId}`);
                            this.dynamoDbClient.delete({ TableName: this.connectionsTableName, Key: { connectionData } });
                        }
                    });
            }));
            this.logger.debug(`All messages have been broadcasted.`);

        } catch (err: any) {
            this.logger.debug("ERROR:", err);
        }
    }
}