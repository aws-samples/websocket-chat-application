// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpErrorResponse } from '@angular/common/http';
import { User } from '../../../../infrastructure-ts/resources/models/user';
import { Channel } from '../../../../infrastructure-ts/resources/models/channel';
import { Message } from '../../../../infrastructure-ts/resources/models/message';
import { AppConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class ApiService {

  jsonHeaders: HttpHeaders;
  constructor(private http: HttpClient, private appConfig: AppConfigService) {
    this.jsonHeaders = new HttpHeaders();
    this.jsonHeaders = this.jsonHeaders.set('Content-Type', 'application/json; charset=utf-8');
  }

  getUsers(): Promise<User[]> {
    return new Promise<User[]>((resolve, reject) => {
      this.http.get<User[]>(this.appConfig.getConfig().api_url + '/users', { observe: 'response' })
        .subscribe(res => {
          resolve(res.body!);
        }, (errorResponse: HttpErrorResponse) => {
          console.log(errorResponse);
          reject(errorResponse);
        });
    });
  }

  getChannelMessages(channelId: string): Promise<Message[]> {
    return new Promise<Message[]>((resolve, reject) => {
      this.http.get<Message[]>(`${this.appConfig.getConfig().api_url}/channels/${channelId}/messages`, { observe: 'response' })
        .subscribe(res => {
          resolve(res.body!);
        }, (errorResponse: HttpErrorResponse) => {
          console.log(errorResponse);
          reject(errorResponse);
        });
    });
  }

  getChannels(): Promise<Channel[]> {
    return new Promise<Channel[]>((resolve, reject) => {
      this.http.get<Channel[]>(this.appConfig.getConfig().api_url + '/channels', { observe: 'response' })
        .subscribe(res => {
          resolve(res.body!);
        }, (errorResponse: HttpErrorResponse) => {
          console.log(errorResponse);
          reject(errorResponse);
        });
    });
  }

  createChannel(channel: Channel): Promise<any> {
    return new Promise<any>((resolve, reject) => {
      this.http.post<any>(this.appConfig.getConfig().api_url + '/channels', JSON.stringify(channel), { headers: this.jsonHeaders })
        .subscribe(res => {
          resolve(res);
        }, (errorResponse: HttpErrorResponse) => {
          console.log(errorResponse);
          reject(errorResponse);
        });
    });
  }
}