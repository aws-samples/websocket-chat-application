// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Injectable } from '@angular/core';
import { Subscriber, Observable, Subject } from 'rxjs';
import ReconnectingWebSocket from '../utils/reconnecting-websocket';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class WebsocketService {

  constructor(private authService: AuthService) { }

  private ws!: ReconnectingWebSocket;
  private observable: Observable<any> | undefined;
  private subject: Subject<any> = new Subject<any>();

  createObservableSocket(url: string,
    openSubscriber: Subscriber<any>,
    closeSubscriber: Subscriber<any>): Subject<any> {

    const options = { debug: false };
    this.ws = new ReconnectingWebSocket(url, [], options);

    new Observable(observer => {
      this.ws.onmessage = event => observer.next(event.data);
      this.ws.onerror = event => observer.error(event);
      this.ws.onclose = event => {
        console.debug("Websocket connection closed");
        closeSubscriber.next();
        closeSubscriber.complete();
      };

      this.ws.onopen = event => {
        console.debug("Websocket connection opened");
        return () => { console.log("observable socket observer returned") };
      }
    }).subscribe((data) => {
      this.subject.next(data);
      console.debug("Payload received: " + JSON.stringify(data));
    });

    return this.subject;
  }

  send(message: any) {
    console.debug("Sending websocket message:");
    console.debug(message);
    this.ws.send(message);
  }
}
