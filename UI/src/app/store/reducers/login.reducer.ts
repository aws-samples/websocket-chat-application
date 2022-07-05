// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { createReducer, on, MetaReducer } from '@ngrx/store';
import { initialState, AppState } from '../states/app.state';
import { loginAction, changeProgressAction, changeWebsocketConnectionStateAction, startupAction } from '../actions/app.actions';
 

// Login action (legacy) ====================================================
const _loginReducer = createReducer(initialState,
    on(loginAction, (state, { userName, password, screen }) => ({ ...state, userName, password, screen })),
    on(changeProgressAction, (state, { isProgressIndicatorVisible }) => ({ ...state, isProgressIndicatorVisible })),
    on(changeWebsocketConnectionStateAction, (state, { isWebsocketOpened }) => ({ ...state, isWebsocketOpened })),
    on(startupAction, (state, {}) => getStateFromLocalStorage())
);

function getStateFromLocalStorage() {
  const previousState = localStorage.getItem('state');
  if (previousState) {
    console.debug('Restoring previous application state.');
    var state: AppState = JSON.parse(previousState);
    state.isProgressIndicatorVisible = false;
    return state;
  }
  return initialState;
}

export function loginReducer(state, action) {
  var newState = _loginReducer(state, action);

  // Always persist application state, except for ngrx reducers
  if (action.type.indexOf('@ngrx') === -1) {
    console.debug('Persisting application state after action:' + action.type);
    localStorage.setItem('state', JSON.stringify(newState));
  }

  return newState;
}