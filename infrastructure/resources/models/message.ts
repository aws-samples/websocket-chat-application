// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Payload } from "./payload";

export class Message extends Payload{
    constructor(init?:Partial<Message>) {
        super("Message");
        Object.assign(this, init);
    }
    
    public sender!: string;
    public text: string | undefined;
    public sentAt: Date | undefined;
    public channelId!: string;
    public messageId!: string;
}