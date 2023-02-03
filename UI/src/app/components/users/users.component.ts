// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Pipe, PipeTransform } from '@angular/core';
import { CommunicationService } from 'src/app/services/communication.service';
import { Subscription } from 'rxjs';
import { MatSnackBar, MatSnackBarHorizontalPosition, MatSnackBarVerticalPosition } from '@angular/material/snack-bar';
import { Component, OnInit } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { User } from '../../../../../infrastructure-ts/resources/models/user';
import { Status } from '../../../../../infrastructure-ts/resources/models/status';
import { AuthService } from 'src/app/services/auth.service';

@Pipe({ name: 'statusPipe' })
export class statusPipe implements PipeTransform {
  transform(status: Status) {
    return Status[status];
  }
}

@Component({
  selector: 'app-users',
  templateUrl: './users.component.html',
  styleUrls: ['./users.component.scss']
})
export class UsersComponent implements OnInit {

  constructor(private apiService: ApiService,
    private _snackBar: MatSnackBar,
    private authService: AuthService,
    private communicationService: CommunicationService) { }

  users: User[] = [];
  private communicationSubstription!: Subscription;
  private horizontalPosition: MatSnackBarHorizontalPosition = 'center';
  private verticalPosition: MatSnackBarVerticalPosition = 'top';

  async ngOnInit() {

    // Call the /users API endpoint to receive a list of users with online/offline statuses
    this.users = await this.apiService.getUsers();

    // This is required because the component could be loaded AFTER receiving the StatusChangeEvent for the current user login event
    let currentUser = this.authService.getUser();
    let userInList = this.users.find(u => u.username === currentUser.username);
    if (userInList) {
      userInList!.status = Status.ONLINE;
    }

    // Sign up for real-time user status change events
    this.communicationSubstription = this.communicationService.getSubscribableSocket().subscribe((t: string) => {
      let payload = JSON.parse(t);
      if (payload.type == "StatusChangeEvent") {

        let user = this.users.find(u => u.username === payload.userId);
        if (user) {
          user.status = payload.currentStatus;
        }
        else {
          this.users.push({ username: payload.userId, status: payload.currentStatus });
        }

        this._snackBar.open(`${payload.userId} is ${Status[payload.currentStatus]}`, "", {
          horizontalPosition: this.horizontalPosition,
          verticalPosition: this.verticalPosition,
          duration: 3000
        });
      }
    });
  }

  ngOnDestroy() {
    this.communicationSubstription.unsubscribe();
  }

  openSnackBar(message: string, action: string) {
    this._snackBar.open(message, action);
  }
}