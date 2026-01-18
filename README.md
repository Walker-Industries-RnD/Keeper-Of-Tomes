<p align="center"> <img src="docs/assets/keeperoftomes.png" alt="Keeper Of Tomes" width="100%"/> </p>

 **A seer for your files, watching over every file and remembering its every change even without constant observation.**  

<p align="center"> The Keeper Of Tomes is a simple yet powerful system which allows developers to keep track of updated files on a directory with a high level of accuracy. It was created for the XRUIOS (Our Cross Platform Framework/OS/Abstraction Layer). </p>

<p align="center">
  <strong>Windows • Linux • Easy To Add Platforms • Fully Offline • Post Quantum Computing Resistant  • No BS</strong>
</p>

## How it works

The **Keeper of Tomes** maintains a cryptographically-verified ledger of every file in a directory:

- **Observed files** – your documents, media, projects (all scanned and tracked)
    
- **Snapshot ledger** – the keeper’s immutable record of file state (hashes, sizes, timestamps)
    

All operations use **async, chunked processing with xxHash64** for fast, deterministic file fingerprints.

The Keeper:

- Records every file’s size, last-modified timestamp, and hash in a snapshot
    
- Detects added, removed, renamed, or modified files automatically
    
- Updates snapshots incrementally — unchanged files are skipped for maximum performance
    
- Guarantees consistency even if files are renamed or partially overwritten during scanning

<br>

<div align="center">

| ![WalkerDev](Assets/WalkerDev.png)  |
|-----------------------------|
| **Code by WalkerDev**<br>“Loving coding is the same as hating yourself”<br>[Discord](https://discord.gg/H8h8scsxtH)  |

</div>

<br>
<p align="center">
  <a href="https://walkerindustries.xyz">Walker Industries</a> •
  <a href="https://discord.gg/H8h8scsxtH">Discord</a> •
  <a href="https://www.patreon.com/walkerdev">Patreon</a>
</p>
<p align="center">
  <a href="https://walker-industries-rnd.github.io/Keeper-Of-Tomes/welcome"><strong>View the Wiki</strong></a> •
</p>
---

## What's In Here
| Focus           | Description                                                      |
| --------------- | ---------------------------------------------------------------- |
| The DLL Project | The main class for syncing directories and managing snapshots    |
| The CMD Projet  | A CMD test for scanning a directory and loading/saving snapshots |

## Dependencies

1. Pariah Cybersecurity + It's Requirements
2. Standart.Hash.xxHash

---


**Note:** The first snapshot creation will always take longer, as every file in the directory must be read and hashed. Subsequent updates are much faster since only changed or new files are processed.


---

## License & Artwork

**Code:** [NON-AI MPL 2.0](https://raw.githubusercontent.com/non-ai-licenses/non-ai-licenses/main/NON-AI-MPL-2.0)  
**Artwork:** — **NO AI training. NO reproduction. NO exceptions.**

<img src="https://github.com/Walker-Industries-RnD/Malicious-Affiliation-Ban/blob/main/WIBan.png?raw=true" align="center" style="margin-left: 20px; margin-bottom: 20px;"/>

> Unauthorized use of the artwork — including but not limited to copying, distribution, modification, or inclusion in any machine-learning training dataset — is strictly prohibited and will be prosecuted to the fullest extent of the law.
