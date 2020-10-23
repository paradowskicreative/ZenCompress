#!/bin/bash

POSITIONAL=()
while [[ $# -gt 0 ]]
do
key="$1"

case $key in
    -q)
    QUALITY="$2"
    shift # past argument
    shift # past value
    ;;
    -comp_level)
    COMPLEVEL="$2"
    shift # past argument
    shift # past value
    ;;
    -output_path)
    OUTPATH="$2"
    shift # past argument
    shift # past value
    ;;
    -file)
    INPATH="$2"
    shift # past argument
    shift # past value
    ;;
    --default)
    DEFAULT=YES
    shift # past argument
    ;;
    *)    # unknown option
    POSITIONAL+=("$1") # save it in an array for later
    shift # past argument
    ;;
esac
done
set -- "${POSITIONAL[@]}" # restore positional parameters

./basisu -q ${QUALITY} -comp_level ${COMPLEVEL} -output_path ${OUTPATH} -file ${INPATH}