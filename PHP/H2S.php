<?php

	//Control port library
	//====================
	//See docs/Library.md for details


	//Define CRLF constant. Whether you like it or not,
	//most text based protocols (HTTP, SMTP, etc) use CRLF.
	//Http2Socks follows this trend too.
	if(!defined('CRLF')){
		define('CRLF',"\r\n");
	}
	//Setting this to H2S_DEBUG to TRUE before including this file makes it print all protocol data.
	//Data is printed as "DBG: --> request" and "DBG: <-- response" to the console.
	//Note: This includes authentication data
	if(!defined('H2S_DEBUG')){
		define('H2S_DEBUG',FALSE);
	}

	//Sends a raw command over the connection
	//returns an array with two values. First is a boolean to indicate success, second the raw response.
	//Use as: list($ok,$data)=H2S_sendCommand(...)
	function H2S_sendCommand($socket,$command){
		//By making the command optional we can use this function to also read the greeting.
		//You never want to do this yourself though as the greeting is the only time where NULL is appropriate.
		if($command!==NULL){
			if(H2S_DEBUG){
				echo "DBG: --> $command" . CRLF;
			}
			fwrite($socket,$command . CRLF);
		}
		$reply=array();
		do{
			$line=rtrim(fgets($socket,1024),CRLF);
			$reply[]=$line;
			if(H2S_DEBUG){
				echo "DBG: <-- $line" . CRLF;
			}
		}while($line!=='OK' && $line!=='ERR' && !feof($socket) && $line!==NULL);
		if($line!==NULL){
			array_pop($reply);
		}
		return array($line==='OK',$reply);
	}

	//Performs authentication using the supplied password or cookie file content
	function H2S_auth($fp,$password){
		list($ok,$result)=H2S_sendCommand($fp,"AUTH $password");
		return $ok;
	}

	//Connects to the given IP and port.
	//The arguments are optional and are the default values for Http2Socks
	function H2S_connect($ip='127.0.0.1',$port=12244,$errno=NULL,$errstr=NULL){
		if($fp=@fsockopen("tcp://$ip",$port,$errno,$errstr,5)){
			list($ok,$greeting)=H2S_sendCommand($fp,NULL);
			if($ok){
				return $fp;
			}
			fclose($fp);
		}
		return FALSE;
	}

	//Gets the blacklist content as associative array.
	//The key is the onion domain, the value an array with the details.
	//The detail keys have the same name as the INI fields from the blacklist ini.
	function H2S_getBlacklist($fp){
		list($ok,$data)=H2S_sendCommand($fp,'BLLIST');
		if($ok){
			$content=parse_ini_string(implode(CRLF,$data),TRUE,INI_SCANNER_RAW);
			return $content;
		}
		return FALSE;
	}

	//Reloads the blacklist from file and replaces the list in memory
	function H2S_reloadBlacklist($fp){
		list($ok,$data)=H2S_sendCommand($fp,'BLRELOAD');
		return $ok;
	}

	//Adds a domain to the blacklist, or updates an existing domain
	function H2S_addToBlacklist($fp,$domain,$code=403,$name=NULL,$notes=NULL,$url=NULL){
		$domain=H2S_encode($domain);
		$code=H2S_encode($code);
		$name=H2S_encode($name);
		$notes=H2S_encode($notes);
		if($url!==NULL && strpos($url,' ')!==FALSE){
			if(H2S_DEBUG){
				echo "Parameter contains a space: $url" . CRLF;
			}
			return FALSE;
		}
		list($ok,$data)=H2S_sendCommand($fp,"BLADD $domain $name $notes $code $url");
		return $ok;
	}

	//Deletes a domain from the blacklist
	function H2S_deleteFromBlacklist($fp,$domain){
		$domain=H2S_encode($domain);
		list($ok,$data)=H2S_sendCommand($fp,"BLREMOVE $domain");
		return $ok;
	}

	//Saves the blacklist to file
	function H2S_saveBlacklist($fp){
		list($ok,$data)=H2S_sendCommand($fp,'BLSAVE');
		return $ok;
	}

	//URL encodes an argument. Useful if an argument contains spaces.
	function H2S_encode($str){
		if($str===NULL){
			return NULL;
		}
		return str_replace('%20','+',urlencode($str));
	}

	//Closes the connection
	function H2S_close($fp){
		list($ok)=H2S_sendCommand($fp,'EXIT');
		fclose($fp);
		return $ok;
	}

	//Gets the protocol version
	function H2S_version($fp){
		list($ok,$data)=H2S_sendCommand($fp,'VERSION');
		if($ok){
			return +$data[0];
		}
		return FALSE;
	}

	//Performs no operation
	function H2S_noop($fp){
		list($ok,$data)=H2S_sendCommand($fp,'NOOP');
		return $ok;
	}
?>