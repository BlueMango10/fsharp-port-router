# Port Router

The purpose of this program is to route traffic from incomming tcp connections on one single port to other "inner ports" on the mashine via localost.

For now, the clients must send their desired inner port as the first message, but any logic could be used to map the client connections to inner ports.

- `Router.fsx` is the actual router program.
- `Server.fsx` is a simple stream-based echo server which lets you specify a port when starting.
- `Client.fsx` is a simple client which connects to the router, then lets you specify an inner port to connect to, and finally lets you send messages on the connection and recieve messages from the connection.
