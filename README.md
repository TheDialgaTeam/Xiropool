# Xiropht-Mining-Pool
Mining pool tool compatible with Xiropht, **released has example tool with an example of website**, this mining pool tool is compatible with Windows/Linux and more.

<h2>Disclamer</h2>

-> **Not updated anymore, you can take it has example to do your own pool**

-> **Released has example, the pool tool can't predict how math calculations are generated from miners.**

-> **You have the freedom to edit the mining pool tool and the official miner compatible with it to provide another way of mining.**

-> **Since the version 0.0.1.3R of the mining pool, the mining logic to generate mining job have change to attempt to fight cheating. This plan has not been tested at 100% and you have to check everything about it to be sure before to use this tool.**

<h2>Description</h2>

-> This mining pool tool don't require to sync blocks to receive the latest blocktemplate.

-> The network protocol used by this tool is the same of the Solo Miner and the Proxy Solo Miner for reach the network.


<h2>Requirements</h2>

-> Netframework 4.6.1 minimum or Mono for other platforms like Linux.

-> Require Xiropht-Connector-All library: https://github.com/XIROPHT/Xiropht-Connector-All

-> Require to setup a RPC Wallet: https://github.com/XIROPHT/Xiropht-RPC-Wallet

-> Require to setup a Remote Node: https://github.com/XIROPHT/Xiropht-Remote-Node


<h2>Compatible miner tools</h2>

<b>Only compatible miner tools work with this tool, solo miner and proxy solo miner are not compatible.</b>

Xiropht-Miner: https://github.com/XIROPHT/Xiropht-Miner

<h2>Website Requirements</h2>

-> Web server using apache, nginx or others.

-> Setting up config.js file.

-> For support HTTPS on the api of the pool, be sure to configure a frontend proxy service like a Nginx Proxy.

<h2>Command lines</h2>

-> API Request list: https://github.com/XIROPHT/Xiropht-Mining-Pool/wiki/Pool---API-HTTP-GET-Request-list

-> Direct command line list:
  - help | Show list of commands details.
  
  - stats | Show mining pool stats.
  
  - banminer |  ban a miner wallet address, syntax: banminer wallet_address time

  - banminerlist | show the list of miner wallet address banned.

  - unbanminer | unban a miner by wallet address.
  
  - exit | Safe exit, Stop mining pool, save and exit.

<h2>Improvements</h2>

**Their is few things who require improvements:**

-> Hashrate calculation estimations done by the pool.

-> Block time estimated to found a block.

<h2>Credits</h2>

**Tool and website programmed by Xiropht Developer (Sam Segura).**

**Design of the website inspired of Cryptonote-Universal-Pool repository: https://github.com/fancoder/cryptonote-universal-pool/tree/master/website**

**Newtonsoft.Json library is used since version 0.0.0.1R for the API HTTP/HTTPS system, and for done json syntax on mining packets: https://github.com/JamesNK/Newtonsoft.Json**
