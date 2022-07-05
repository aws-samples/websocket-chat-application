// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0


// User model representing the JWT content
export class UserModel {
    public aud!: string;
    public auth_time!: string;
    public username!: string;
    public email!: string;
    public exp!: number;
    public iat!: number;
    public iss!: string;
    public sub!: string;
    public token!: string;
}
