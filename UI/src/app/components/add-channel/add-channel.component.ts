// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';

export interface DialogData {
  name: string;
}

@Component({
  selector: 'app-add-channel',
  templateUrl: './add-channel.component.html',
  styleUrls: ['./add-channel.component.scss']
})
export class AddChannelComponent {

  constructor(public dialogRef: MatDialogRef<AddChannelComponent>,
    @Inject(MAT_DIALOG_DATA) public data: DialogData,) { }

  onNoClick(): void {
    this.dialogRef.close();
  }
}
