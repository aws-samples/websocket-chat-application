// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Stack, StackProps } from 'aws-cdk-lib'
import { Construct } from 'constructs';
import { Color, Dashboard, GraphWidget, Metric } from 'aws-cdk-lib/aws-cloudwatch';

export class ObservabilityStack extends Stack {

  constructor(scope: Construct, id: string, props?: StackProps) {
    super(scope, id, props);

    const disconnectionsMetric = new Metric({
      namespace: 'websocket-chat',
      metricName: 'closedConnection',
      statistic: 'sum'
    });

    const newcConnectionsMetric = new Metric({
      namespace: 'websocket-chat',
      metricName: 'newConnection',
      statistic: 'sum'
    });

    const messagesDeliveredMetric = new Metric({
      namespace: 'websocket-chat',
      metricName: 'messageDelivered',
      statistic: 'sum'
    });


    var closedConnectionsWidget = new GraphWidget({
      title: "Closed Connections",
      width: 12,
      left: [disconnectionsMetric.with({
        color: Color.RED
      })]
    });

    var newConnectionsWidget = new GraphWidget({
      title: "New Connections",
      width: 12,
      left: [newcConnectionsMetric.with({
        color: Color.GREEN
      })]
    });

    var messagesDeliveredWidgets = new GraphWidget({
      title: "Messages Delivered",
      width: 24,
      left: [messagesDeliveredMetric.with({
        color: Color.GREEN
      })]
    });

    const dashboard = new Dashboard(this, "Serverless Websocket Chat Dashboard", {
      widgets: [
        [
          newConnectionsWidget,
          closedConnectionsWidget
        ],
        [
          messagesDeliveredWidgets,
        ]
      ]
    });
  }
};