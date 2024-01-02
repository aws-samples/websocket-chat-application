namespace Shared.Tests;

public class Tests
{
    private string validTokenString =
        @"eyJraWQiOiJTczU3Mzk2c09Oa0FsV01Wa253a3hIK2djN1dnY1wvSGFrZG9tY0JDY2F3VT0iLCJhbGciOiJSUzI1NiJ9.eyJhdF9oYXNoIjoiRWZNdnUzYnoyU3RmcHpWdWpmZ29CQSIsInN1YiI6ImY2OTg5MTk1LTE3YTUtNDY3NC1iNTc4LWQ3Y2Q1M2MyMjczNSIsImF1ZCI6IjRnbmI1amhnbDYxOWI2ZWNkMDc5dDJzdWdlIiwiZXZlbnRfaWQiOiI4ODM1YWY4Zi0zNTc2LTQ5YmMtOWFkZi0wNmM4NzE2NmVkYjYiLCJ0b2tlbl91c2UiOiJpZCIsImF1dGhfdGltZSI6MTY3MjIzODQyOSwiaXNzIjoiaHR0cHM6XC9cL2NvZ25pdG8taWRwLmV1LXdlc3QtMi5hbWF6b25hd3MuY29tXC9ldS13ZXN0LTJfazBkamJIVElCIiwiY29nbml0bzp1c2VybmFtZSI6InRhbWFzc2FudGEiLCJleHAiOjE2NzIyODE2MjksImlhdCI6MTY3MjIzODQyOSwianRpIjoiYTI1YjI4ZDEtY2EzZS00NGQxLTgzNzUtOTA2NzA5OTE5YjQ4In0.uuRadN3eaTWNjwMPfhhO9VgZnMRD5nu8mUOjhfHZo8JNaxblx6IzLhgsBjM4zsZKuJRpDDgQYt5dcjhJujRHL187QP1Vg81SL-3hyYskMDP7Ph3jF52xel8J8Hba03xkqUTVqKL71HXBmMmJoFxwCXDXMSioaI6YkNJfiIMtuINQ5t3QreP9PrsZ5u7U2_KEfEzPcFAyoa1oqRR_ssSRZogAhhRzSdGMXxnFDhxMvkwFomUNt6tXtTXjxnJuDB6LknnODfjAHGTWYq7DO3wOl0WpwUfinxw9h25o_-_XRHtdwuA9twcxGEJioq2l4KYUcrQO715b1mzQ1YWFUfrPEA";

    private string validUserpoolId = "eu-west-2_k0djbHTIB";

    private string validClientId = "4gnb5jhgl619b6ecd079t2suge";
    
    [SetUp]
    public void Setup()
    {
        
    }

    [Test]
    public void CanReadValidToken()
    {
        var verifier = new CognitoJwtVerifier(validUserpoolId, validClientId, "eu-west-2");
        var result = verifier.VerifyToken(validTokenString);
        Assert.Pass();
    }
}