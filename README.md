# pi in a pod

```sh
run-pi.cs
```

Runs a preconfigured pi-session in the current working directory, mounted inside a docker container.
It's very opinionated, tailored to my taste. I'll see if I keep using it.

## Installation

Clone the repo and you're ready to go. You'll probably want to add a symlink:

```sh
ln -s /path/to/repo/run-pi.cs ~/.local/bin/run-pi.cs
```

## Usage

Only a single argument is supported: `--force-build` forces a rebuild of the container. Use this
when a new version of pi is released.
