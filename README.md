# SimpleAirDefense

## Introduction

SimpleAirDefense is a _very_ simple IADS (Integrated Air Defense) service
to demonstrate the kind of things that can be done with DCS-gRPC.

This IADS service switches on SAM sites if an enemy enters range of a SAM
site while being tracked by an EWR site. It does not have Line-of-sight checks
or any other more advanced features like shutdown-on-HARM launch or radar
blinking.

## Configuration

Update the `configuration.yaml` file with the connection information required
to connect to your DCS-gRPC instance(s)