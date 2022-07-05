// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { CookieService } from 'ngx-cookie-service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-logout',
  template: ""
})
export class LogoutComponent implements OnInit {

  constructor(private router: Router,
    private cookieService: CookieService,
    private authService: AuthService) { }

  ngOnInit() {
    console.log("Deleting ID token....");
    this.authService.token = "";
    this.cookieService.delete('id_token');
    this.router.navigate(['/login']);
  }
}
