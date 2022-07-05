// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Router } from '@angular/router';
import { Injectable } from '@angular/core';
import { HttpEvent, HttpInterceptor, HttpHandler, HttpRequest, HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../services/auth.service';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';


@Injectable()
export class AuthInterceptor implements HttpInterceptor {

    constructor(private authService: AuthService, private router: Router) {}

    intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
        if (this.authService.isLoggedIn()) {
            const authReq = req.clone({ setHeaders: { Authorization: this.authService.getUser().token } });
            return next.handle(authReq).pipe(catchError(this.handleError.bind(this)));
        }
        return next.handle(req);
    }

    handleError(error: HttpErrorResponse) {
        console.debug(`There was an error handling the response: ${JSON.stringify(error)}`);
        if (error.status === 401) {
            this.router.navigate(['/timeout']);
        }
        return throwError(error);
    }
}
