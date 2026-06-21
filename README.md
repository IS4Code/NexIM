NexIM
==========

The _NexIM_ project is a software suite aimed to bring extensive chatting capabilities to .NET,
providing a fast, secure, and reliable solution for federated instant messaging that is easy to setup and run for anyone. 

## Mission statement

The current state of the instant messaging landscape is not satisfactory:
while the mainstream space becomes dominated by a singular walled garden product every decade,
the federated web is fragmented among several protocols and communities, with varying degrees of interoperability.
It becomes obvious that inventing a new protocol that solves the flaws of all the previous ones
is simply not a viable solution, as doing so only further separates the already isolated communities.

Instead, this project proposes a different solution: to provide a _universal server_,
capable of serving _any_ suitable contemporary protocol. This entails:

* Clients, components, and remote servers are free to utilize any protocol they prefer.
* Instead of trying to express everything through the lens of a single protocol, like most bridges do, the server accommodates for all of them.
* Business logic runs fully abstracted from concrete protocols, operating via an Abstract Eventing Protocol.
* Support for the individual protocols is achieved by various adapter layers.
* Protocol-level extensions are always preserved, meaning two clients using the same protocol can communicate without the server stripping parts of their communication.

Support for these protocols is envisioned:

* XMPP,
* IRC,
* ActivityPub and RSS (and other semantic web technologies like WebMention),
* MIME (e-mail, via SMTP and other protocols),
* Matrix.

Of course, the project, being open-source, is welcoming to additions of other protocols not listed here.

## IM organization

The server implementation takes a fixed stance on organizing communities, mimicking the common semi-social hierarchies found on other platforms:

* The server itself (such as `example.net`) serves for authentication and direct interaction between users.
* Users (such as `user@example.net`) are the primary actors within the server and the originators of various events.
* Realms (such as `games.example.net`) offer spaces for users to join and seek others, and to group related channels.
* Channels (such as `online@games.example.net`) are where users can meet and chat.
