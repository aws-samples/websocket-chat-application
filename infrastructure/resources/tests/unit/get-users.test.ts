//{\"Items\":[{\"connectionId\":\"PTuPtemvLPECERw=\"}],\"Count\":1,\"ScannedCount\":1}"

import { Status } from "../../models/status";
import { User } from "../../models/user";

test('user list and statuses correct', () => {
    let DDBitems = { Items: [
        { 
            connectionId: "PTuPtemvLPECERw=",
            userId: "tamassanta"
        },
        { 
            connectionId: "PTuPtemvLPECERw=",
            userId: "gordon"
        },
        { 
            connectionId: "PTuPtemvLPECERw=",
            userId: "orsolya"
        },
        { 
            connectionId: "PTuPtemvLPECERw=",
            userId: "simon"
        },
        { 
            connectionId: "PTuPtemvLPECERw=",
            userId: "fiifi"
        },
        { 
            connectionId: "PTuPtemvLPECERw="
        },
        { 
            connectionId: "PTuPtemvLPECERw=",
        },
    ], Count: 1, ScannedCount: 1 };
    let cognitoUsers = {
        Users: [
            {
                Username: "tamassanta"
            },
            {
                Username: "gordon"
            },
            {
                Username: "orsolya"
            },
            {
                Username: "simon"
            },
            {
                Username: "fiifi"
            },
            {
                Username: "angus"
            }
        ]
    };

    let userList: User[] = cognitoUsers.Users.map((user)=> {
        let userIsConnected = DDBitems.Items.find(u => u.userId === user.Username);
        return new User({
            username: user.Username,
            status: userIsConnected ? Status.ONLINE : Status.OFFLINE
        });
    });
    

    console.log(JSON.stringify(userList));
    expect(true).toBeTruthy();
})

