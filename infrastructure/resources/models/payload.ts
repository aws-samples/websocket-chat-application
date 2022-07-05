// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

// Generic payload wrapper for websocket communication.
// Helps identifying the different models exchanged over the wire.
export class Payload {
    constructor(public type: string) {
    }
}