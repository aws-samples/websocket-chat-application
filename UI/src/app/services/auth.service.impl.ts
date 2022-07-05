// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { JwtHelper } from './../utils/jwthelper';
import { UserModel } from './../models/user';
import { Injectable } from '@angular/core';
import { AuthService } from './auth.service';
import { CookieService } from 'ngx-cookie-service';

@Injectable({
    providedIn: 'root'
})
export class AuthServiceImpl implements AuthService {
    private user: UserModel | undefined;
    public token: string | undefined;

    constructor(private jwtHelper: JwtHelper,
        private cookieService: CookieService) { }

    setToken(token: string): void {
        this.token = token;
        this.user = this.loadUser(this.token);
    }

    loadUser(token: string): UserModel {
        var user = this.jwtHelper.decodeToken(token);
        user.token = token;
        user.username = user["cognito:username"];

        return user;
    }

    getUser(): UserModel {
        if (this.user == undefined && this.hasToken()) {
            this.user = this.loadUser(this.token!);
        }
        return this.user!;
    }

    hasToken(): boolean {
        if (this.token == "" || this.token == null) {
            console.debug("token not set, looking for cookies...");
            var cookie = this.cookieService.get("id_token");
            if (cookie != null && cookie != "" && !this.jwtHelper.isTokenExpired(cookie)) {
                console.debug("cookie has been found, setting user token");
                this.setToken(cookie);
            }
        }
        return this.token ? true : false;
    }

    isLoggedIn(): boolean {
        return this.hasToken() && !this.jwtHelper.isTokenExpired(this.token!);
    }

    clearToken(): void {
        this.token = undefined;
    }
}
