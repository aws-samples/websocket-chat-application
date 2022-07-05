// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

export interface AppState {
    userName: string;
    password: string;
    isProgressIndicatorVisible: boolean;
    isWebsocketOpened: boolean;
}

export const initialState : AppState = {
    userName: "",
    password: "",
    isProgressIndicatorVisible: false,
    isWebsocketOpened: false
}