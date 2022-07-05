// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { Action, createAction, props } from '@ngrx/store';

export const loginAction = createAction(
    '[Auth/API] Login Success',
    props<{  userName: string; password: string; screen: any; }>()
  );

export const changeProgressAction = createAction(
  '[App] Change progress indicator state',
  props<{ isProgressIndicatorVisible: boolean;  }>()
);

export const changeWebsocketConnectionStateAction = createAction(
  '[App] Change websocket connection state',
  props<{ isWebsocketOpened: boolean;  }>()
);

//export const shutdownAction = createAction('[App] Shutdown', props<{ }>());
//export const startupAction = createAction('[App] Startup', props<{ }>());