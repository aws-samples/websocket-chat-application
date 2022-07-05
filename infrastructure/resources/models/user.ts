// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Status } from "./status";

export class User {
    constructor(init?:Partial<User>) {
        Object.assign(this, init);
    }
    public username!: string;
    public status!: Status;
}