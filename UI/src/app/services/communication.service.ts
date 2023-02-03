// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Injectable } from '@angular/core';
import { WebsocketService } from './websocket.service';
import { Subscriber, Subject } from 'rxjs';
import { Store } from '@ngrx/store';
import { AppState } from '../store/states/app.state';
import { changeWebsocketConnectionStateAction } from '../store/actions/app.actions';
import { Payload } from '../../../../infrastructure-ts/resources/models/payload';
import { AppConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class CommunicationService {

  observableSocket: Subject<any>;

  constructor(private webSocket: WebsocketService, private appConfig: AppConfigService, private store: Store<{ login: AppState }>) {
    const openSubscriber = Subscriber.create(() => {
      console.log('connection opened');
      this.store.dispatch(changeWebsocketConnectionStateAction({
        isWebsocketOpened: true
      }));
    });
    const closeSubscriber = Subscriber.create(() => {
      this.store.dispatch(changeWebsocketConnectionStateAction({
        isWebsocketOpened: false
      }));
    });
    this.observableSocket = this.webSocket.createObservableSocket(this.appConfig.getConfig().broadcast_url, openSubscriber, closeSubscriber);
  }

  getSubscribableSocket(): Subject<any> {
    return this.observableSocket;
  }

  sendMessage(request: Payload) {
    console.log("Sending payload: " + JSON.stringify(request));
    var message = { action: 'payload', data: request };
    this.webSocket.send(JSON.stringify(message));
  }
}
