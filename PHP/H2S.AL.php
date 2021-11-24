<?php
	//Alias list specific commands


	//Gets the alias list content as associative array.
	//The key is the onion domain, the value an array with the details.
	//The detail keys have the same name as the INI fields from the alias ini.
	function H2S_getAliaslist($fp){
		list($ok,$data)=H2S_sendCommand($fp,'ALLIST');
		if($ok){
			$content=parse_ini_string(implode(CRLF,$data),TRUE,INI_SCANNER_RAW);
			return $content;
		}
		return FALSE;
	}

	//Reloads the alias list from file and replaces the list in memory
	function H2S_reloadAliaslist($fp){
		list($ok,$data)=H2S_sendCommand($fp,'ALRELOAD');
		return $ok;
	}

	//Adds a domain to the Aliaslist, or updates an existing domain
	function H2S_addToAliaslist($fp,$domain,$alias,$code=0){
		$domain=H2S_encode($domain);
		$alias=H2S_encode($alias);
		$code=H2S_encode($code);
		list($ok,$data)=H2S_sendCommand($fp,"ALADD $domain $alias $code");
		return $ok;
	}

	//Deletes a domain from the Aliaslist
	function H2S_deleteFromAliaslist($fp,$domain){
		$domain=H2S_encode($domain);
		list($ok,$data)=H2S_sendCommand($fp,"ALREMOVE $domain");
		return $ok;
	}

	//Saves the Aliaslist to file
	function H2S_saveAliaslist($fp){
		list($ok,$data)=H2S_sendCommand($fp,'ALSAVE');
		return $ok;
	}
