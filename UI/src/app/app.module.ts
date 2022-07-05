// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { APP_INITIALIZER, NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { AppRoutingModule } from './app-routing.module';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { StoreModule } from '@ngrx/store';
import { CookieService } from 'ngx-cookie-service';
import { JwtHelper } from './utils/jwthelper';
import { AuthService } from './services/auth.service';
import { AuthServiceImpl } from './services/auth.service.impl';

import { AppComponent } from './app.component';
import { statusPipe, UsersComponent } from './components/users/users.component';
import { MessagesComponent } from './components/messages/messages.component';
import { ChannelsComponent } from './components/channels/channels.component';
import { LoginComponent } from './components/login/login.component';

import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatRadioModule } from '@angular/material/radio';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTabsModule } from '@angular/material/tabs';
import { MatGridListModule } from '@angular/material/grid-list';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthCallbackComponent } from './components/auth-callback/auth-callback.component';
import { ApiService } from './services/api.service';
import { AddChannelComponent } from './components/add-channel/add-channel.component';
import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { AuthGuardService } from './guards/auth.guard';
import { AuthInterceptor } from './interceptors/auth.interceptor';
import { AppConfigService } from './services/config.service';

const appInitializerFn = (appConfig: AppConfigService) => {
  return () => {
    return appConfig.loadAppConfig();
  };
};

@NgModule({
  providers: [ApiService, CookieService, JwtHelper,
    {
      provide: AuthService, useClass: AuthServiceImpl
    },
    {
      provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true
    },
    AppConfigService,
    {
      provide: APP_INITIALIZER,
      useFactory: appInitializerFn,
      multi: true,
      deps: [AppConfigService]
    },
    AuthGuardService],
  declarations: [
    statusPipe,
    AppComponent,
    UsersComponent,
    MessagesComponent,
    ChannelsComponent,
    LoginComponent,
    AuthCallbackComponent,
    AddChannelComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    BrowserAnimationsModule,
    MatCardModule,
    MatRadioModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatToolbarModule,
    MatSidenavModule,
    MatCheckboxModule,
    MatTabsModule,
    MatCardModule,
    MatGridListModule,
    MatButtonModule,
    MatInputModule,
    MatListModule,
    MatIconModule,
    MatSidenavModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatDialogModule,
    HttpClientModule,
    FormsModule,
    MatSnackBarModule,
    StoreModule.forRoot({  }),
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
