// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Payload } from "./payload"
import { Status } from "./status";

export class StatusChangeEvent extends Payload {
    constructor(init?:Partial<StatusChangeEvent>) {
        super("StatusChangeEvent");
        Object.assign(this, init);
    } 
    public userId!: string;
    public currentStatus!: Status;
    public eventDate!: Date;
}