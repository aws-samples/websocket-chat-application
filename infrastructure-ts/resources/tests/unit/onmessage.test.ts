import * as JWT from "jsonwebtoken/index";
import { CognitoJwtVerifier } from "aws-jwt-verify";

test('jwt is valid', async () => {

  let token = "eyJraWQiOiJvekxzcVZvWnJSVyt6NmFFR0VcL1o4SUt4NFF6VzRzR0s1Nkt2dnlXaFFIWT0iLCJhbGciOiJSUzI1NiJ9.eyJhdF9oYXNoIjoiTkc3VmQzU1B4ZXVNSE5ha1FKX3VtUSIsInN1YiI6IjYyNjJmMDVjLTVhYjAtNDM4Mi1iODliLTEyNjYwMWFmNDNiZiIsImF1ZCI6IjcwbzM1OGpiYTZsMWg2bDZncmozYXA5ajRkIiwiZXZlbnRfaWQiOiJiYzg5NDRlNS0yOGExLTRiZmUtYTRkMS03YWVkNGM1ZmFmOWMiLCJ0b2tlbl91c2UiOiJpZCIsImF1dGhfdGltZSI6MTY0NzcxNjMyOSwiaXNzIjoiaHR0cHM6XC9cL2NvZ25pdG8taWRwLmV1LXdlc3QtMi5hbWF6b25hd3MuY29tXC9ldS13ZXN0LTJfYnZ3dm9JRHUyIiwiY29nbml0bzp1c2VybmFtZSI6InRhbWFzc2FudGEiLCJleHAiOjE2NDc3NTk1MjksImlhdCI6MTY0NzcxNjMyOSwianRpIjoiNzNiMWVlODItOWY1OS00NGMyLTkwZjQtM2QzZTFhY2UxNzAxIn0.I5FSmBVLO6wsbGtW6SyxIJsFGp9dSBvCFh22E6E_82FoCW-_IpRyLHv455R9Q0qthi-sYPvbqXaoetiBCuABczeUD7g8dsSLIoIW36W4lM2AaiPkM2rNmlQL21jrCAJF08FsSVsDxeHH1mCrp3yZc8T9uXqnZ33WH9bUHNobTeH2o6_pn8skHzsWwehLDnJscG1oVeZwbMG4smr3CcypgQCh6Qw1LYLab6eAPYKuVjZeBKrioc54PhXyF1pgRlav5OKXuiL7oKmE2df76QfqHmuJdo2ftUGqAradeMldcyv-ft6a8duBHZRrrju5k36MedFwgLrCO1trs-gnVrG4eg";

  let cognitoVerifier = CognitoJwtVerifier.create({
    userPoolId: "eu-west-2_bvwvoIDu2",
    tokenUse: "id",
    clientId: "70o358jba6l1h6l6grj3ap9j4d"
  });

  let result = await cognitoVerifier.verify(token);
  console.log(result);
  const jwt_token = JWT.decode(token) as JWT.JwtPayload;
  console.log(JSON.stringify(jwt_token));

  expect(true).toBeTruthy();
});