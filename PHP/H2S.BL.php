<?php
	//Blacklist specific commands


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
	function H2S_addToBlacklist($fp,$domain,$name=NULL,$notes=NULL,$code=403,$url=NULL){
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
