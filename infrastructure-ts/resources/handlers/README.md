# API Request Handlers
In this folder you can find the request handler lambda functions for both APIs (REST/Websocket). The lambda functions are written in Typescript. They are transpiled and packaged by the CDK runtime.

----

## REST API handlers

### **get-channel-messages**  
`[GET] /channels/{ID}/messages`

Retrieves ALL messages from the messages DynamoDB table by {channelId}.
Please note, that **this will not scale** as eventually the response will reach the maximum size limit. 
Possible improvement options could be:
* limit the number of items to the last X messages
* implement paging

### **get-channel**
`[GET] /channels/{ID}`
Retrieves a channel from the channels DynamoDB table by {name}.

### **get-channels**
Retrieves ALL channels from the channels DynamoDB table.
Please note, that **this will not scale** as eventually the response will reach the maximum size limit. 
Possible improvement options could be:
* limit the number of items to the last X channels
* implement paging

### **get-config**
Retrieves the configuration for the frontend. This API endpoint and handles is called every time the SPA executes the Angular bootstrap process.
The parameters are stored in SSM Parameter Store.

### **get-users**
`[GET] /users`
This handler retrieves ALL users from the Cognito userpool, then enriches that information with online/offline statuses.

### **post-channels**
`[POST] /channels/`
Inserts a new channel into the channels DynamoDB table.

----

## Websocket API handlers
### **authorizer**
Implements a cookie based Cognito JWT token authorizer. The cookies are sent by the browser on the first HTTPS call, before upgrading the websocket connection. The return value of this function is an IAM PolicyDocument, with **Allow** or **Deny** for the api endpoint.
    
### **onconnect**
`$connect`
Inserts the connectionId, and the clientId into the connections DynamoDB table. This is later used to broadcast messages back to the user.

### **ondisconnect**
`$disconnect`
Called when the websocket connection closes (by either side). Removed the associated connectionId record from the connections DynamoDB table.

### **onmessage**
Handles actual websocket messages.

    
    


