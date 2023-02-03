// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Component, OnInit, Input } from '@angular/core';
import { Subscription } from 'rxjs';
import { Message } from '../../../../../infrastructure-ts/resources/models/message';
import { CommunicationService } from 'src/app/services/communication.service';
import { Channel } from '../../../../../infrastructure-ts/resources/models/channel';
import { ApiService } from 'src/app/services/api.service';
import { AuthService } from 'src/app/services/auth.service';
import { UserModel } from 'src/app/models/user';

@Component({
  selector: 'app-messages',
  templateUrl: './messages.component.html',
  styleUrls: ['./messages.component.scss']
})
export class MessagesComponent implements OnInit {

  private channel!: Channel;
  @Input() set Channel(channel: Channel) {
    this.channel = channel;
    this.loadChannel(channel);
  }
  get Channel(): Channel {
    return this.channel;
  }

  private communicationSubstription!: Subscription;

  public messages: Message[] = [];

  public messageText: string = "";
  public user!: UserModel;
  constructor(private communicationService: CommunicationService,
    private apiService: ApiService,
    private authService: AuthService) { }

  async ngOnInit() {
    this.communicationSubstription = this.communicationService.getSubscribableSocket().subscribe((t: string) => {
      let payload = JSON.parse(t);
      if (payload.type == "Message") {

        let message = payload as Message;
        if(message.channelId == this.channel.id) {
          this.messages.push(payload);
        }
      }
    });

    this.user = this.authService.getUser();
  }

  async loadChannel(channel: Channel) {
    await this.apiService.getChannelMessages(channel.id).then(c => { this.messages = c });
  }

  onDestroy() {
    this.communicationSubstription.unsubscribe();
  }

  async sendMessage(text: string) {
    console.log("Sending message:" + text);
    let message = new Message({
      sender: this.user.username,
      text: text,
      channelId: this.Channel.id,
      sentAt: new Date()
    });

    this.communicationService.sendMessage(message);
    this.messageText = "";
  }
}
