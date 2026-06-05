
# How to Import a VM into OpenShift from a `.qcow2` file:

To import your exported VM disk image (like a `.qcow2`, `.img`, or `.raw` file) back into OpenShift Virtualization, you can use the Containerized Data Importer (CDI).

The easiest and most common way to do this is by using the `virtctl` CLI tool to upload the file directly into a new DataVolume/PersistentVolumeClaim (PVC), which you then attach to a VM.

##Prerequisites:##

- You must have the `virtctl` tool installed and configured with cluster access
- The file must be accessible from your local machine
- Ensure you have enough storage capacity in your target storage class

## Step 1: Upload the Disk Image using `virtctl`:

The `virtctl image-upload` command automates creating a DataVolume and pushing your local file directly into the OpenShift cluster.

Run the following command:
```
virtctl image-upload dv dotnet-legacy-disk \
  --size=20Gi \
  --image-path=./vm-disk.qcow2 \
  --storage-class=ocs-storagecluster-ceph-rbd \
  --access-mode=ReadWriteOnce \
  --namespace=dotnet-legacy
  ```

Key Parameters Breakdown:

`dv my-imported-disk`: Creates a DataVolume named my-imported-disk.
`--size`: Specify the size of the disk (ensure it is equal to or larger than the original virtual disk size).
`--image-path`: The local path to your exported disk file.
`--storage-class`: The storage class you want to use (e.g., Ceph, local-storage, etc.).
`--access-mode`: Typically `ReadWriteOnce` (RWO) or `ReadWriteMany` (RWX) depending on your storage capability and if you need live migration.
> [!NOTE]
> If your cluster uses self-signed certificates for the CDI upload proxy, you may need to append the `--insecure` flag to the command.

## Step 2: Verify the Upload

Before creating the VM, check that the upload completed successfully and the DataVolume is in a `Succeeded` state:
```
oc get dv dotnet-legacy-disk -n dotnet-legacy
```

## Step 3: Create the VM Using the Imported Disk
Once the DataVolume is ready, you need to create a Virtual Machine configuration that references this disk. You can do this via the OpenShift Web Console or via a YAML manifest.

## Option A: Using the OpenShift Web Console (Easiest)

1. Log in to the OpenShift Web Console and switch to the Administrator perspective.
2. Navigate to Virtualization -> VirtualMachines and click Create -> With Wizard (or From Manifest).
3. Choose your OS template.
4. When configuring Disks, instead of creating a new root disk, select Use existing PVC or Clone existing PVC.
5. Choose the `dotnet-legacy-disk` PVC you just uploaded.
6. Review and click Create VirtualMachine.

## Option B: Using a YAML Manifest

Create a file named `imported-vm.yaml` and reference the DataVolume as a template volume source:
```
apiVersion: kubevirt.io/v1
kind: VirtualMachine
metadata:
  name: dotnet-legacy-win2k22
  namespace: dotnet-legacy
spec:
  running: false
  template:
    metadata:
      labels:
        kubevirt.io/domain: dotnet-legacy-win2k22
    spec:
      domain:
        cpu:
          cores: 4
        memory:
          guest: 16Gi
        devices:
          disks:
            - disk:
                bus: virtio
              name: rootdisk
      volumes:
        - name: rootdisk
          persistentVolumeClaim:
            claimName: dotnet-legacy-disk
```

Apply the manifest to start the VM creation process:
```
oc create -f imported-vm.yaml
```

## Step 4: Start the VM
Once the VM is created, you can boot it up using `virtctl`:
```
virtctl start my-imported-vm -n my-namespace
```


# How to Export a VM from OpenShift into a file (already done for you under the name `vm-disk.qcow2`):

To export a Virtual Machine (VM) from OpenShift Virtualization, you will use the VirtualMachineExport API. This process allows you to extract the VM's Persistent Volume Claims (PVCs) or snapshots so you can download the disk images or move them to another cluster.  

There are the two main ways to do it: the quick way using the virtctl CLI, and the declarative way using a YAML manifest, which is the preferred method used below:

##Prerequisites:##

- The VM must be shut down before you begin
- You must have the OpenShift CLI (oc) tool installed

## Step 1: Create the Manifest:

### Create a file named `vm-export.yaml`:
```
apiVersion: export.kubevirt.io/v1beta1
kind: VirtualMachineExport
metadata:
  name: dotnet-legacy-vm-export
  namespace: dotnet-legacy
spec:
  source:
    apiGroup: "kubevirt.io"
    kind: VirtualMachine
    name: dotnet-legacy-win2k22
  ttlDuration: 2h
```
> [!NOTE]
> `ttlDuration` dictates how long the export links will stay alive before OpenShift automatically cleans up the resource (default is 2 hours).

## Step 2: Apply the Manifest:
```
oc create -f vm-export.yaml
```

## Step 3: Retrieve the Links and Token:

Once applied, OpenShift will generate secret tokens and internal/external URLs for your disk images. Check the status by running:
```
oc get vmexport dotnet-legacy-vm-export -n dotnet-legacy -o yaml
```

Look at the `status.links` section. It will display `external` or `internal` URLs to download the manifest or individual volume images.

## Step 4: Download via `curl`:

To download the disk image using the external link, you must extract the authentication token generated by the export:

1. **Save the certificate:**
```
oc get vmexport dotnet-legacy-vm-export -n dotnet-legacy -o jsonpath={.status.links.external.cert} > cacert.crt
```

2. **Decode the secret token:**
```
oc get secret export-token-dotnet-legacy-vm-export -n my-namespace -o jsonpath={.data.token} | base64 --decode > token.txt
```

3. **Download the image:**
```
curl --cacert cacert.crt -H "x-kubevirt-export-token: $(cat token.txt)" -H "Accept: application/yaml" <EXTERNAL_MANIFEST_URL> -o vm-disk.qcow2
```

(Replace `<EXTERNAL_MANIFEST_URL>` with the specific volume URL provided in the `status.links` output).

## Clean Up:
It is not necessary to wait for the TTL used in the export manifest to expire. You can manually delete the export object to clean up the token secrets and routes:
```
oc delete vmexport my-vm-export -n my-namespace
```
