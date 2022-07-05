// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Injectable()
export class AppConfigService {
  private appConfig: any;

  constructor(private http: HttpClient) { }

  loadAppConfig() {
    // for local development, use the local config file
    /*return this.http.get ('/assets/appConfig.json')
      .toPromise()
      .then(data => {
        this.appConfig = data;
      });*/

    return this.http.get('api/config')
      .toPromise()
      .then(data => {
        this.appConfig = data;
      });
  }

  getConfig() {
    return this.appConfig;
  }
}
