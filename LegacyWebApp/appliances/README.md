# Windows Server 2022 VM Disk Image

This directory contains the configuration and instructions for deploying a pre-configured Windows Server 2022 VM with IIS and the LegacyWebApp already installed.

## Quick Start: Download Pre-Built VM Disk

**Skip the export process!** Download the ready-to-use qcow2 image:

```bash
cd ~/mta-dotnet-demo/LegacyWebApp/appliances

# Download the pre-built VM disk image (~54GB compressed)
aws s3 cp s3://mta-dotnet-demo-aromerot/vm-disk.qcow2 ./vm-disk.qcow2

# Or using curl (if publicly accessible)
# curl -o vm-disk.qcow2 https://mta-dotnet-demo-aromerot.s3.amazonaws.com/vm-disk.qcow2
```

**What's included:**
- Windows Server 2022 (fully patched)
- IIS configured with .NET Framework 4.8
- LegacyWebApp deployed and running
- Guest login credentials: `administrator` / `openshift123!`

---

# How to Import a VM into OpenShift from a `.qcow2` file:

To import your exported VM disk image (like a `.qcow2`, `.img`, or `.raw` file) back into OpenShift Virtualization, you can use the Containerized Data Importer (CDI).

The easiest and most common way to do this is by using the `virtctl` CLI tool to upload the file directly into a new DataVolume/PersistentVolumeClaim (PVC), which you then attach to a VM.

## Prerequisites:

- You must have the `virtctl` tool installed and configured with cluster access
- The VM disk image file (download from S3 as shown above, or export your own)
- Ensure you have enough storage capacity in your target storage class

## Step 1: Upload the Disk Image using `virtctl`:

The `virtctl image-upload` command automates creating a DataVolume and pushing your local file directly into the OpenShift cluster.

Run the following command:
```bash
cd ~/mta-dotnet-demo/LegacyWebApp/appliances

virtctl image-upload dv dotnet-legacy-disk \
  --size=60Gi \
  --image-path=./vm-disk.qcow2 \
  --storage-class=ocs-storagecluster-ceph-rbd \
  --access-mode=ReadWriteMany \
  --namespace=dotnet-legacy \
  --force-bind
```

Key Parameters Breakdown:

- `dv dotnet-legacy-disk`: Creates a DataVolume named dotnet-legacy-disk
- `--size`: Size of the disk (60Gi for this VM)
- `--image-path`: Path to your qcow2 disk file
- `--storage-class`: Your cluster's storage class (e.g., `gp3-csi`, `ocs-storagecluster-ceph-rbd`, etc.)
- `--access-mode`: Typically `ReadWriteOnce` (RWO) or `ReadWriteMany` (RWX) depending on your storage capability and if you need live migration.
- `--force-bind`: Force immediate PVC binding (required for some storage classes)
- `--insecure`: Skip certificate validation (needed for self-signed certs)

> [!NOTE]
> If your cluster uses self-signed certificates for the CDI upload proxy, you may need to append the `--insecure` flag to the command.
> The upload will take 30-60 minutes depending on your network speed. The virtctl command will show progress.

## Step 2: Verify the Upload

Before creating the VM, check that the upload completed successfully and the DataVolume is in a `Succeeded` state:
```bash
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

Create a file named `vm-import.yaml` and reference the DataVolume as a template volume source:
```
apiVersion: kubevirt.io/v1
kind: VirtualMachine
metadata:
  name: dotnet-legacy-win2k22
  namespace: dotnet-legacy
spec:
  runStrategy: Halted
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
        firmware:
          bootloader:
            efi:
              persistent: true
              secureBoot: true
        features:
          acpi: {}
          apic: {}
          smm:
            enabled: true
        devices:
          disks:
            - disk:
                bus: sata
              name: rootdisk
      volumes:
        - name: rootdisk
          persistentVolumeClaim:
            claimName: dotnet-legacy-disk
        - cloudInitNoCloud:
            userData: |
              #cloud-config
              user: administrator
              password: openshift123!
          name: cloudinitdisk
```

Apply the manifest to start the VM creation process:
```bash
oc create -f vm-import.yaml
```

## Step 4: Start the VM
Once the VM is created, you can boot it up using `virtctl`:
```bash
virtctl start dotnet-legacy-win2k22 -n dotnet-legacy
```

# How to Export a VM from OpenShift into a file (already done for you under the name `vm-disk.img.gz`):

To export a Virtual Machine (VM) from OpenShift Virtualization, you will use the VirtualMachineExport API. This process allows you to extract the VM's Persistent Volume Claims (PVCs) or snapshots so you can download the disk images or move them to another cluster.  

There are the two main ways to do it: the quick way using the virtctl CLI, and the declarative way using a YAML manifest, which is the preferred method used below:

## Prerequisites:

- The VM must be shut down before you begin
- You must have the OpenShift CLI (oc) tool installed

# Option 1:

## Step 1: Create helper pod:
Create a helper pod bound to the same volume as the VM to help with the copy of the disk since `virtctl vmexport` may fail:
```
apiVersion: v1
kind: Pod
metadata:
  name: disk-export-helper
  namespace: dotnet-legacy
spec:
  containers:
  - name: helper
    image: registry.access.redhat.com/ubi9/ubi:latest
    command: ["/bin/sh", "-c", "sleep infinity"]
    volumeDevices:
    - name: vm-disk
      devicePath: /dev/vmdisk
  volumes:
  - name: vm-disk
    persistentVolumeClaim:
      claimName: dotnet-legacy-win2k22
```

## Step 2: Copy the contents of the volume in the helper pod locally:
```bash
cd ~/mta-dotnet-demo/LegacyWebApp/appliances

oc exec -n dotnet-legacy disk-export-helper -- sh -c "dd if=/dev/vmdisk bs=4M | gzip" > vm-disk.img.gz
```
> [!NOTE]
> You can verify the vm-disk.img.gz file size with `ls -lh ./vm-disk.img.gz`

## Step 3: Unzip and convert `.img` to `.qcow2`
```bash
gunzip -c vm-disk.img.gz

qemu-img convert -f raw -O qcow2 -c -p vm-disk.img.gz vm-disk.qcow2
```
> [!NOTE]
> You may need to install `qemu` (Mac: `brew install qemu`) or `qemu-img` (Fedora: `dnf install -y qemu-img`)

# Option 2:

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
```bash
oc create -f vm-export.yaml
```

## Step 3: Retrieve the Links and Token:

Once applied, OpenShift will generate secret tokens and internal/external URLs for your disk images. Check the status by running:
```bash
cd ~/mta-dotnet-demo/LegacyWebApp/appliances

oc get vmexport dotnet-legacy-vm-export -n dotnet-legacy -o yaml
```

Look at the `status.links` section. It will display `external` or `internal` URLs to download the manifest or individual volume images.

## Step 4: Download via `curl`:

To download the disk image using the external link, you must extract the authentication token generated by the export:

1. **Save the certificate:**
```bash
oc get vmexport dotnet-legacy-vm-export -n dotnet-legacy -o jsonpath='{.status.links.external.cert}' > cacert.crt
```

2. **Decode the secret token:**
```bash
oc get secret export-token-dotnet-legacy-vm-export -n dotnet-legacy -o jsonpath='{.data.token}' | base64 --decode > token.txt
```

3. **Capture the Disk URL and Download the image:**
```bash
DISK_URL=$(oc get vmexport dotnet-legacy-vm-export -n dotnet-legacy -o jsonpath='{.status.links.external.volumes[0].formats[0].url}')

curl --cacert cacert.crt \
  -H "x-kubevirt-export-token: $(cat token.txt)" \
  -H "Accept: application/gzip" \
  --max-time 0 \
  --keepalive-time 60 \
  "$DISK_URL" \
  -o vm-disk.img.gz
```
> [!NOTE]
> The variable `$DISK_URL` contains the specific VM volume URL provided in the `status.links` output).

## Clean Up:
It is not necessary to wait for the TTL used in the export manifest to expire. You can manually delete the export object to clean up the token secrets and routes:
```bash
oc delete vmexport dotnet-legacy-vm-export -n dotnet-legacy
```
