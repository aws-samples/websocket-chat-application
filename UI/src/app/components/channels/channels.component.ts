import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { ApiService } from 'src/app/services/api.service';
import { Channel } from '../../../../../infrastructure-ts/resources/models/channel';
import { AddChannelComponent } from '../add-channel/add-channel.component';

export interface Section {
  name: string;
  member_count: number;
}

@Component({
  selector: 'app-channels',
  templateUrl: './channels.component.html',
  styleUrls: ['./channels.component.scss']
})
export class ChannelsComponent implements OnInit {
  public channels!: Channel[];
  public selectedChannel!: Channel;
  private _channelSelections!: Channel[];
  get channelSelections(): Channel[] {
      return this._channelSelections;
  }
  set channelSelections(value: Channel[]) {
      this._channelSelections = value;
      if(this._channelSelections.length > 0)
      {
        this.selectedChannel = this._channelSelections[0];
      }
  }

  constructor(public dialog: MatDialog, 
              public apiService: ApiService,
              public router: Router) { }

  async ngOnInit() {
    this.refreshChannels();
  }

  logout() {
    this.router.navigate(['/logout']);
  }

  async refreshChannels() {
    await this.apiService.getChannels().then(c => {this.channels = c});
  }

  openDialog(): void {
    const dialogRef = this.dialog.open(AddChannelComponent, {
      width: '250px',
      data: { },
    });

    dialogRef.afterClosed().subscribe(async result => {
      console.log('The dialog was closed');
      console.log(result);
      if(result !== undefined) {
        await this.apiService.createChannel({id: result, Participants:[]})
        this.refreshChannels();
      }
    });
  }
}