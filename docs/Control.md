# Control Connection Protocol

The control connection is a simple line based protocol that allows control of the service to some degree. It's used to perform certain actions without having to restart the service.

## Version

See `VERSION` command.

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

Example: `COMMAND Argument1 Argument2 Argument3`

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

Note: A connection generally won't time out. This command is only necessary if the traffic is routed across problematic NAT devices.

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

- Arguments: `<domain> <name> <notes> <type> <url>`
- Action: Adds or updates the given line to the memory pool of blackisted onion services
- Response: Success if the line was added sucessfully
- Authentication: Yes
- Example: `BLADD example.onion Evil+Services  451 https://reason.example.com/reason/for/451`

"Name" and "Notes" can be URL encoded to handle spaces. Other characters can also be URL encoded but it's not necessary. For a list of valid types, check the blacklist chapter.

Note: To skip arguments, don't specify it but still add the space character. The command example doesn't specifies internal notes, so there's just two spaces in succession

Note: Entries are not saved to file immediately. See `BLSAVE` for how to do this.

Note: This command replaces any existing blacklist entry for the given domain in memory

## Command `BLREMOVE`

- Arguments: onion domain
- Action: Removes the given onion service from memory
- Response: Success if service was found and removed
- Authentication: Yes
- Example: `BLREMOVE example.onion`

Note: Entries are not deleted from the file immediately. See `BLSAVE` for how to do this.

## Command `BLSAVE`

- Arguments: None
- Action: Saves the blacklist in memory to the blacklsit file
- Response: Success if file could be written
- Authentication: Yes
- Example: `BLSAVE`

This will save all entries in memory to the blacklist file.
Note that this will discard any comments present in the file previously.
For this command to work, "Blacklist" must be configured in the DNS section.
