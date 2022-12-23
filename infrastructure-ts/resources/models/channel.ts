// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { User } from "./user";

export class Channel {
    public id!: string;
    public Participants!: User[];
}