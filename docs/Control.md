# Control Connection Protocol

The control connection is a simple line based protocol that allows control of the service to some degree. It's used to perform certain actions without having to restart the service.

`H2S.php` in the PHP folder implements all commands.

## Terms in this document

This document uses "long.onion" as a way to express a full v3 onion address.

## Version

Obtainable via: `VERSION` command.

This value is incremented if an incompatible change to the API is made.

The current version is: `1`

## Line Format

Lines are formatted using UTF-8. The line ending is CRLF (ASCII `0x0D` followed by ASCII `0x0A`).

## Connection

The protocol uses TCP. Upon connection you will receive a greeting.
The greeting is formatted like any other response but otherwise has no special format.
If the greeting is an error message, the connection will be terminated and the user should try again later.

## Command

A command is an upper case string, followed by optional arguments.
Delimiter for arguments is the space.
If an argument is optional it can be skipped by not supplying it but still leave the space intact.
This is not necessary at the end of the argument list.

- Command with 3 arguments: `COMMAND Argument1 Argument2 Argument3`
- Same command, but argument 2 is skipped: `COMMAND Argument1  Argument3`

Commands will generally accept any extra arguments to make them forward compatible.
Some commands only work when the connection is authenticated.

## Arguments

Arguments normally do not support escaping.
If an argument can potentially contain spaces, URL encoding can be performed.

## Response

Every command triggers a response. A response consists of at least one line.
The last line of a response is always "OK" or "ERR" to indicate success or failure as well as the end of the response.

## Command `NOOP`

- Arguments: None
- Action: Does nothing
- Response: Always success
- Authentication: Ignored
- Example: `NOOP`

Note: A connection generally won't time out. This command is only necessary if the traffic is routed across problematic NAT devices. In that case you want to fire off the command about every 18 seconds.

## Command `EXIT`

- Arguments: None
- Action: Closes the connection
- Response: Always success
- Authentication: Ignored
- Example: `EXIT`

The server will close the connection immediately after sending the reply.

## Command `AUTH`

- Arguments: Password or cookie file contents
- Action: Autheticates the connection
- Response: Success on sucessful authentication
- Authentication: No
- Example: `AUTH MyPasswordGoesHere`

Note: No special treatment needed for passwords that contain spaces. The command will fail if already authenticated.

## Command `VERSION`

- Arguments: None
- Action: Responds with the control connection version
- Response: Always success
- Authentication: Ignored
- Example: `VERSION`

The version is a simple integer that specifies the API version.
An application must accept multiple numbers on the same line to support future extensions to the protocol.

## Command `INFO`

- Arguments: None
- Action: Responds with process status
- Response: Always success
- Authentication: Ignored
- Example: `INFO`

Process status is provided in the form of `Name=Value` pairs.
Booleans are represented using `1` and `0` for `true` and `false`.

Known keys:

- AUTH: true, if authenticated
- HALT: true, if HTTP processing is halted
- AL: Number of alias entries
- BL: Number of blacklist entries
- ALFILE: true, if alias file is specified (and thus, `ALSAVE` will work)
- BLFILE: true, if blacklist file is specified (and thus, `BLSAVE` will work)

Note: Except for "AUTH", keys are only shown when authenticated

## Command `HALT`

- Arguments: None
- Action: Halts HTTP processing
- Response: Always success
- Authentication: Yes
- Example: `HALT`

This halts all HTTP processing at the point where the alias list or blacklist is used in the request pipeline.
This means that completely invalid requests are still processed.

This command is best used for when modifications to the alias or blacklist are made and no requests should fail because of the short time frame where entries may be missing from the list.

Note: Connections are still accepted and will "pile up" until `CONT` is issued.
To halt processing more efficiently, the service should be paused or stopped if the control connection is not needed.

**CAUTION! Request processing does not automatically resume when you close/drop the control connection.**
**If there's a processing error, connect again and issue the CONT command to resume processing.**
**Restarting the service also resets the HALT flag.**

## Command `CONT`

- Arguments: None
- Action: Continues HTTP processing
- Response: Always success
- Authentication: Yes
- Example: `CONT`

Continues HTTP request processing that was previously stopped using `HALT`

## Command `BLRELOAD`

- Arguments: None
- Action: Reloads the blacklist file from disk
- Response: Success if the file was reloaded. Error if the file could not be found/read
- Authentication: Yes
- Example: `BLRELOAD`

This command guarantees that memory will not corrupt or be wiped.
Only when the file was successfully read and parsed will the blacklist in memory update.

Note: Clears the list in memory if no blacklist file is configured.

## Command `BLLIST`

- Arguments: None
- Action: Lists the contents of the blacklist from memory
- Response: Always success
- Authentication: Yes
- Example: `BLLIST`

The response is in INI format (see format of blacklist file)

Note: This is the contents from memory and not the file on disk. The actual file will not be read.

## Command `BLADD`

- Arguments: `<domain> [name] [notes] [type] [url]`
- Action: Adds or updates the given line to the memory pool of blackisted onion services
- Response: Success if the line was added sucessfully
- Authentication: Yes
- Example: `BLADD long.onion Evil+Services  451 https://reason.example.com/reason/for/451`

Defaults:

- name: Empty string
- notes: Empty string
- type: 403
- url: Empty string

"Name" and "Notes" can be URL encoded to handle spaces. Other characters can also be URL encoded but it's not necessary. For a list of valid types, check the blacklist chapter.

Note: Entries are not saved to file immediately. See `BLSAVE` for how to do this.

Note: This command replaces any existing blacklist entry for the given domain in memory

## Command `BLREMOVE`

- Arguments: onion domain
- Action: Removes the given onion service from memory
- Response: Success if service was found and removed
- Authentication: Yes
- Example: `BLREMOVE long.onion`

Note: Entries are not deleted from the file immediately. See `BLSAVE` for how to do this.

## Command `BLSAVE`

- Arguments: None
- Action: Saves the blacklist in memory to the blacklist file
- Response: Success if file could be written
- Authentication: Yes
- Example: `BLSAVE`

This will save all entries in memory to the blacklist file.
Note that this will discard any comments present in the file previously.
For this command to work, "Blacklist" must be configured in the DNS section.

## Command `ALRELOAD`

- Arguments: None
- Action: Reloads the alias file from disk
- Response: Success if the file was reloaded. Error if the file could not be found/read
- Authentication: Yes
- Example: `ALRELOAD`

This command guarantees that memory will not corrupt or be wiped.
Only when the file was successfully read and parsed will the alias list in memory update.

Note: Clears the list in memory if no alias file is configured.

## Command `ALLIST`

- Arguments: None
- Action: Lists the contents of the alias list from memory
- Response: Always success
- Authentication: Yes
- Example: `ALLIST`

The response is in INI format (see format of alias file)

Note: This is the contents from memory and not the file on disk. The actual file will not be read.

## Command `ALADD`

- Arguments: `<domain> <alias> [type]`
- Action: Adds or updates the given line to the memory pool of aliased onion services
- Response: Success if the line was added sucessfully
- Authentication: Yes
- Example: `ALADD long.onion alias.onion 1`

Defaults:

- type: 0

For a list of valid types, check the alias chapter.

Note: Entries are not saved to file immediately. See `ALSAVE` for how to do this.

Note: This command replaces any existing alias entry for the given domain in memory

Note: A domain can only have a single alias

## Command `ALREMOVE`

- Arguments: onion domain
- Action: Removes the given onion service from memory
- Response: Success if service was found and removed
- Authentication: Yes
- Example: `ALREMOVE long.onion`

Note: Entries are not deleted from the file immediately. See `ALSAVE` for how to do this.

## Command `ALSAVE`

- Arguments: None
- Action: Saves the alias list in memory to the alias file
- Response: Success if file could be written
- Authentication: Yes
- Example: `ALSAVE`

This will save all entries in memory to the alias file.
Note that this will discard any comments present in the file previously.
For this command to work, "Alias" must be configured in the DNS section.
