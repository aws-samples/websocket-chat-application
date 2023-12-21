// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Injectable } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { HttpUrlEncodingCodec } from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class AuthGuardService  {
  constructor(
    public authService: AuthService,
    public router: Router,
    public activeRoute: ActivatedRoute) { }

  canActivate(): boolean {
    console.log(window.location.href);
    console.log(window.location.pathname);
    var urlEncoder = new HttpUrlEncodingCodec();
    if (!this.authService.hasToken()) {
      console.log("user is not logged in - redirecting to login page");
      this.router.navigate(['/login'], { queryParams: { redirect_url: urlEncoder.encodeValue(window.location.pathname) } });

      return false;
    }
    else if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/timeout']);
      return false;
    }
    return true;
  }
}
