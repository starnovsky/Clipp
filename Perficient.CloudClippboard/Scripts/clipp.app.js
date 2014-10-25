var maxRetries = 3;
var blockLength = 131072;//1048576;
var retryAfterSeconds = 3;

var g_totalBlocks = 0;
var g_currentBlock = 0;
var g_filesToUpload = 0;

$(function () {
    jQuery.event.props.push('dataTransfer');

    // Reference the auto-generated proxy for the hub.
	var clipp = $.connection.clippHub;

	// Create a function that the hub can call back to display messages.
	clipp.client.pushText = function (message) {
		// Add the message to the page.
        $('#message').val(message);
	};

	clipp.client.notifyUploaded = function () {
	    listFiles();
	};

	// Start the connection.
	$.connection.hub.start().done(function () {
		clipp.server.setKey(uniqueKey);

		$("#message").on('keyup onchange', function () {
		    clipp.server.sendText(uniqueKey, $('#message').val());
		});		
	});

	$(document).on("change", "#selectFile", function () {
	    var fileControl = document.getElementById("selectFile");
	    if ($(fileControl).val() == "") {
	        return;
	    }

	    beginUpload(fileControl.files);

	    $(fileControl).val("");
	});

	$("#droptarget").on("dragover", function () { $(this).removeClass('alert-info').addClass('alert-success'); return false; });
	$("#droptarget").on("dragend", function () { $(this).removeClass('alert-success').addClass('alert-info');; return false; });
	$("#droptarget").on("drop", function (event) {
	    event.preventDefault && event.preventDefault();
	    $(this).removeClass('alert-success').addClass('alert-info');

	    var files = event.dataTransfer.files;
	    beginUpload(files);

	    return false;
	});

	$("#cliboardKeyChange").click(function (e) {
	    window.location.href = "/" + $("#clipboardKey").val();
	});

	$("#cliboardKeyRefresh").click(function (e) {
	    window.location.href = "/";
	});

	$("#progressPanel").hide();
	$("#alertPanel").hide();
	$('#message').focus();

	listFiles();
});

function listFiles()
{
    var payload = { key: uniqueKey };
    $.ajax({
        type: "POST",
        async: true,
        url: "/Clipp/ListFiles",
        cache: false,
        data: payload
    }).done(function (list) {
        $("#fileList").empty();
        $.each(list, function ()
        {
            $("#fileList").append('<li class="list-group-item"><button class="btn btn-xs btn-danger delete-file pull-right" type="button" image="' + this.ImageName + '">delete</button><a href="' + this.Url + '">' + this.FileName + '</a></li>');
        });
        $(".delete-file").click(function (e) {
            e.preventDefault();
            var image = $(this).attr("image");
            deleteFile(image);
        });
    }).fail(function () {
        displayStatusMessage("Failed to send list files");
    });
}

function deleteFile(name) {
    var payload = { key: uniqueKey, imageName: name };
    $.ajax({
        type: "POST",
        async: true,
        url: "/Clipp/DeleteFile",
        cache: false,
        data: payload
    }).fail(function () {
        displayStatusMessage("Failed to delete file");
    });
}

var beginUpload = function (files) {
        if (files.length > 0) {
            g_filesToUpload = files.length;
            g_totalBlocks = 0;
            g_currentBlock = 1;
            for (var i = 0; i < files.length; i++) {
                uploadMetaData(files[i], i);
            }
    }
}

var uploadMetaData = function (file, index) {
    var numberOfBlocks = Math.ceil(file.size / blockLength);
    var payload = { fileName: file.name, key: uniqueKey };

    g_totalBlocks += numberOfBlocks;

    updateProgress(g_currentBlock, g_totalBlocks);

    $.ajax({
        type: "POST",
        async: false,
        url: "/Clipp/SetMetadata",
        cache: false,
        data: payload
    }).done(function (imageName) {
        if (imageName) {
            sendFile(file, blockLength, imageName, numberOfBlocks);
        }
    }).fail(function () {
        displayStatusMessage("Failed to send MetaData");
    });

}

var sendFile = function (file, chunkSize, imageName, numberOfBlocks) {
    var start = 0,
        end = Math.min(chunkSize, file.size),
        retryCount = 0,
        sendNextChunk, fileChunk;

    displayStatusMessage("");

    sendNextChunk = function (currentChunk) {
        fileChunk = new FormData();

        if (file.slice) {
            fileChunk.append('Slice', file.slice(start, end));
        }
        else if (file.webkitSlice) {
            fileChunk.append('Slice', file.webkitSlice(start, end));
        }
        else if (file.mozSlice) {
            fileChunk.append('Slice', file.mozSlice(start, end));
        }
        else {
            displayStatusMessage(operationType.UNSUPPORTED_BROWSER);
            return;
        }

        fileChunk.append("key", uniqueKey);
        fileChunk.append("id", currentChunk);
        fileChunk.append("fileType", file.type);
        fileChunk.append("imageName", imageName);
        fileChunk.append("numberOfBlocks", numberOfBlocks);

        jqxhr = $.ajax({
            async: true,
            url: ('/Clipp/UploadChunk'),
            data: fileChunk,
            cache: false,
            contentType: false,
            processData: false,
            type: 'POST'
        }).fail(function (request, error) {
            if (error !== 'abort' && retryCount < maxRetries) {
                ++retryCount;
                setTimeout(sendNextChunk, retryAfterSeconds * 1000);
            }

            if (error === 'abort') {
                displayStatusMessage("Aborted");
            }
            else {
                if (retryCount === maxRetries) {
                    displayStatusMessage("Upload timed out.");
                }
                else {
                    displayStatusMessage("Resuming Upload");
                }
            }

            return;
        }).done(function (notice) {
            if (notice.error) {
                displayStatusMessage(notice.message);
                return;
            }
            if (notice.isLastBlock) {
                g_filesToUpload--;
                updateProgress(g_currentBlock, g_totalBlocks, g_filesToUpload == 0);
                return;
            }
            ++currentChunk;
            ++g_currentBlock;
            start = (currentChunk - 1) * blockLength;
            end = Math.min(currentChunk * blockLength, file.size);
            retryCount = 0;
            updateProgress(g_currentBlock, g_totalBlocks);
            if (currentChunk <= numberOfBlocks) {
                sendNextChunk(currentChunk);
            }
        });
    }

    sendNextChunk(1);
}

var displayStatusMessage = function (message) {
    if (message.length < 1) {
        $("#alertPanel").hide();
    }
    else {
        $("#alertPanel").show();
    }
    $("#alertPanel").text(message);
}

var updateProgress = function (currentChunk, numberOfBlocks, isComplete) {
    var progress = parseInt(currentChunk / numberOfBlocks * 100);
    if (progress <= 100) {
        $("#progressPanel").show();
        $("#progressBar").attr("style", "width: " + progress + "%");
        $("#progressText").text(progress + "% complete");

        if (isComplete) {
            $("#progressBar").attr("style", "width: 100%");
            setTimeout(function () { $("#progressPanel").hide(); }, 600);
        }
    }
}