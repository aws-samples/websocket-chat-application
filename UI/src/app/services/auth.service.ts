// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0

import { UserModel } from '../models/user';

export abstract class AuthService {
    public abstract setToken(token: string): void;
    public abstract getUser(): UserModel;
    public abstract hasToken(): boolean;
    public abstract isLoggedIn(): boolean;
    public abstract clearToken(): void;
    public token: string | undefined;
}
