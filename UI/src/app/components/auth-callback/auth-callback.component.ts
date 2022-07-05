// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { CookieService } from 'ngx-cookie-service';
import { HttpParams } from '@angular/common/http';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'auth-callback',
  template: ""
})
export class AuthCallbackComponent implements OnInit {

  constructor(private router: Router,
    private cookieService: CookieService,
    private authService: AuthService) { }

  ngOnInit() {
    const httpParams = new HttpParams({ fromString: this.router.url.split('#')[1] });
    console.log(httpParams.get('id_token'));
    this.authService.setToken(httpParams.get('id_token')!);
    this.cookieService.set('id_token', httpParams.get('id_token')!);
    this.router.navigate(['/channels']);
  }
}
