import { Component, Inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { environment } from 'src/environments/environment';
import { DOCUMENT } from '@angular/common';
import { AppConfigService } from 'src/app/services/config.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {

  constructor(@Inject(DOCUMENT) private document: Document, private appConfig: AppConfigService) { }

  async onSubmitClick(name:string, pass:string) {};
  
  ngOnInit(): void {
    console.log(this.appConfig.getConfig().login_url);
    this.document.location.href = this.appConfig.getConfig().login_url;
  }

}
